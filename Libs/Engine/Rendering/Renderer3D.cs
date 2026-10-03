using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Engine.Assets.Dasset;
using Engine.Animation;
using Engine.World;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// Collects and submits 3D draw requests for one render pass.
/// </summary>
public sealed class Renderer3D : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct DirectVertexUniforms
    {
        public Matrix4x4 WorldViewProjection;
        public Matrix4x4 World;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct InstancedVertexUniforms
    {
        public Matrix4x4 ViewProjection;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct ShadowDirectVertexUniforms
    {
        public Matrix4x4 WorldLightViewProjection;
    }

    private sealed class DrawItem
    {
        public required Mesh Mesh;
        public required Material Material;
        public Matrix4x4 World;
        public RenderState3D State;

        /// <summary>局部空间 AABB；null 表示不做视锥剔除（实例化 draw 等拿不到 bounds 的场景，保守提交）。</summary>
        public DassetBounds? LocalBounds;

        /// <summary>蒙皮 draw 的 joint palette（非 null 即蒙皮），提交时写入材质 vertex uniform slot 2。</summary>
        public Matrix4x4[]? JointPalette;
        public VertexBuffer? InstanceBuffer;
        public Material? InstancedDepthMaterial;
        public int InstanceCount;
        public int Sequence;

        /// <summary>透明队排序用：world 平移分量到相机位置的平方距离，End() 分队后填入。</summary>
        public float DistanceSq;

        public bool IsInstanced => InstanceBuffer != null;
    }

    /// <summary>不透明队比较器：材质 → mesh → 是否实例化 → 提交顺序（hash 分组减少管线状态切换）。</summary>
    private sealed class OpaqueDrawComparer : IComparer<DrawItem>
    {
        public static readonly OpaqueDrawComparer Instance = new();

        public int Compare(DrawItem? left, DrawItem? right)
        {
            var materialOrder = RuntimeHelpers.GetHashCode(left!.Material)
                .CompareTo(RuntimeHelpers.GetHashCode(right!.Material));
            if (materialOrder != 0)
                return materialOrder;

            var meshOrder = RuntimeHelpers.GetHashCode(left.Mesh)
                .CompareTo(RuntimeHelpers.GetHashCode(right.Mesh));
            if (meshOrder != 0)
                return meshOrder;

            var instanceOrder = left.IsInstanced.CompareTo(right.IsInstanced);
            return instanceOrder != 0 ? instanceOrder : left.Sequence.CompareTo(right.Sequence);
        }
    }

    private sealed class TransparentDrawComparer : IComparer<DrawItem>
    {
        public static readonly TransparentDrawComparer Instance = new();

        public int Compare(DrawItem? left, DrawItem? right)
            => CompareBackToFront(left!.DistanceSq, left.Sequence, right!.DistanceSq, right.Sequence);
    }

    private interface IInstanceBufferPool : IDisposable
    {
        void BeginFrame();
    }

    private sealed class InstanceBufferPool<TInstance> : IInstanceBufferPool
        where TInstance : unmanaged, IVertex
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<VertexBuffer<TInstance>> _buffers = [];
        private int _used;

        public InstanceBufferPool(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public VertexBuffer<TInstance> Rent()
        {
            if (_used == _buffers.Count)
                _buffers.Add(new VertexBuffer<TInstance>(
                    _graphicsDevice,
                    $"Renderer3D-{typeof(TInstance).Name}-{_buffers.Count}"));

            var buffer = _buffers[_used++];
            buffer.Clear();
            return buffer;
        }

        public void BeginFrame()
        {
            _used = 0;
        }

        public void Dispose()
        {
            foreach (var buffer in _buffers)
                buffer.Dispose();
            _buffers.Clear();
        }
    }

    private readonly GraphicsDevice _graphicsDevice;
    private readonly List<DrawItem> _items = [];
    private readonly Dictionary<Type, IInstanceBufferPool> _instanceBufferPools = [];
    private IDrawableTarget? _target;
    private Camera3D? _camera;
    private int _sequence;
    private bool _disposed;
    private IDrawableTarget? _shadowTarget;
    private Material? _shadowMaterial;
    private Material? _skinnedShadowMaterial;
    private SceneLighting3D? _lighting;
    private CascadedShadowMap? _cascades;
    private RectInt? _shadowViewport;
    private Matrix4x4 _shadowLightViewProjection;

    public bool IsActive => _target != null;

    /// <summary>上一帧 End() 中被视锥剔除跳过的 draw 数（诊断/UI 用）。</summary>
    public int LastFrameCulledCount { get; private set; }

    public Renderer3D(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>
    /// 为本帧启用阴影 pass：End() 会先把已收集的 draw 提交到 <paramref name="shadowTarget"/>
    /// （用 <paramref name="depthMaterial"/>，面剔除翻转为 Front 消 acne），再提交颜色 pass。
    /// 须在 Begin 之后、End 之前调用。depthMaterial 的顶点 uniform 需为单个
    /// WorldLightViewProjection 矩阵（见 DepthOnly.hlsl）。
    /// 实例化 draw 在提供匹配布局的 instancedDepthMaterial 时进入阴影 pass。
    /// 提供 skinnedDepthMaterial 时蒙皮 draw 使用它，并将 palette 写入 vertex slot 2。
    /// </summary>
    public void SetShadowPass(
        IDrawableTarget shadowTarget,
        Material depthMaterial,
        in Matrix4x4 lightViewProjection,
        Material? skinnedDepthMaterial = null)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(shadowTarget);
        ArgumentNullException.ThrowIfNull(depthMaterial);
        if (!ReferenceEquals(shadowTarget.GraphicsDevice, _graphicsDevice))
            throw new InvalidOperationException("Shadow target and Renderer3D must belong to the same GraphicsDevice.");

        _shadowTarget = shadowTarget;
        _shadowMaterial = depthMaterial;
        _skinnedShadowMaterial = skinnedDepthMaterial;
        _shadowLightViewProjection = lightViewProjection;
    }

    public void SetShadowPass(CascadedShadowMap cascades, Material depthMaterial, Material? skinnedDepthMaterial = null)
    {
        SetShadowPass(cascades.Target, depthMaterial, cascades.Matrices[0], skinnedDepthMaterial);
        _cascades = cascades;
    }

    /// <summary>只对本次 Begin/End 生效，提交 draw 时统一写入标准管线的光照槽位。</summary>
    public void SetLighting(SceneLighting3D lighting)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(lighting);
        _lighting = lighting;
    }

    public void DrawModel(DassetModelAsset model, IReadOnlyList<(Material, RenderState3D)> materials,
        in Matrix4x4 world, ReadOnlySpan<Matrix4x4> palette = default, int meshIndex = -1)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(materials);
        if (materials.Count != model.Primitives.Count)
            throw new ArgumentException("Material count must match model primitives.", nameof(materials));
        for (var i = 0; i < model.Primitives.Count; i++)
        {
            if (meshIndex >= 0 && i != meshIndex)
                continue;
            var primitive = model.Primitives[i];
            var (material, state) = materials[i];
            if (!primitive.IsSkinned)
            {
                Draw(primitive.Mesh, material, world, state, primitive.Bounds);
                continue;
            }
            // 无动画组件也应绘制 bind pose，而不是静默丢掉蒙皮 primitive。
            var joints = palette;
            if (joints.IsEmpty)
            {
                var skeleton = model.Skeletons[primitive.SkinIndex];
                var bind = new Matrix4x4[Math.Min(skeleton.Joints.Count, SkeletonAnimator.MaxJoints)];
                SkeletonAnimator.ComputePalette(skeleton, null, 0f, bind);
                joints = bind;
            }
            Draw(primitive.Mesh, material, world, state, joints, primitive.Bounds);
        }
    }

    /// <summary>
    /// Starts collecting draw requests for a target and camera.
    /// </summary>
    public void Begin(IDrawableTarget target, Camera3D camera)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(camera);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_target != null)
            throw new InvalidOperationException("Renderer3D.Begin was called while a pass is active.");
        if (!ReferenceEquals(target.GraphicsDevice, _graphicsDevice))
            throw new InvalidOperationException("Renderer3D and render target must belong to the same GraphicsDevice.");

        _target = target;
        _camera = camera;
        _sequence = 0;
        foreach (var pool in _instanceBufferPools.Values)
            pool.BeginFrame();
    }

    /// <summary>
    /// Queues one mesh transform for a direct draw shader.
    /// </summary>
    public void Draw(Mesh mesh, Material material, in Matrix4x4 world)
        => Draw(mesh, material, world, RenderState3D.Opaque);

    /// <summary>
    /// Queues one mesh transform for a direct draw shader, with an explicit render state
    /// （透明状态会进透明队列，End() 时按 back-to-front 在不透明队之后提交）。
    /// <paramref name="localBounds"/> 提供局部空间 AABB 时做视锥剔除；无 bounds 的 draw 保守地总是提交。
    /// </summary>
    public void Draw(Mesh mesh, Material material, in Matrix4x4 world, in RenderState3D state, in DassetBounds? localBounds = null)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        EnsureMeshDevice(mesh);

        _items.Add(new DrawItem
        {
            Mesh = mesh,
            Material = material,
            World = world,
            State = state,
            LocalBounds = localBounds,
            Sequence = _sequence++
        });
    }

    /// <summary>
    /// Queues one skinned mesh transform for a direct draw shader：joint palette 随 draw 拷贝一份
    /// （调用侧的数组可能被后续帧复用）。材质须用蒙皮变体 shader（Standard3DSkinned），
    /// palette 在提交时写入 vertex uniform slot 2。
    /// </summary>
    public void Draw(Mesh mesh, Material material, in Matrix4x4 world, in RenderState3D state,
        ReadOnlySpan<Matrix4x4> jointPalette, in DassetBounds? localBounds = null)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        EnsureMeshDevice(mesh);

        _items.Add(new DrawItem
        {
            Mesh = mesh,
            Material = material,
            World = world,
            State = state,
            LocalBounds = localBounds,
            JointPalette = PadPalette(jointPalette),
            Sequence = _sequence++
        });
    }

    /// <summary>cbuffer 必须完整上传；剩余关节填 identity，避免驱动读取短 buffer 越界。</summary>
    public static Matrix4x4[] PadPalette(ReadOnlySpan<Matrix4x4> palette)
    {
        if (palette.Length > SkeletonAnimator.MaxJoints)
            throw new ArgumentOutOfRangeException(nameof(palette));
        var result = new Matrix4x4[SkeletonAnimator.MaxJoints];
        Array.Fill(result, Matrix4x4.Identity);
        palette.CopyTo(result);
        return result;
    }

    /// <summary>
    /// Queues one mesh transform for a direct draw shader.
    /// </summary>
    public void Draw(Mesh3D mesh, Material material, in Matrix4x4 world)
        => Draw(mesh.Geometry, material, world);

    /// <summary>
    /// Queues one mesh transform for a direct draw shader, with an explicit render state.
    /// </summary>
    public void Draw(Mesh3D mesh, Material material, in Matrix4x4 world, in RenderState3D state)
        => Draw(mesh.Geometry, material, world, state);

    /// <summary>
    /// Queues one mesh transform for a direct draw shader, with an explicit render state
    /// and a local-space AABB for frustum culling（通常传 <see cref="Mesh3D.Bounds"/>）。
    /// </summary>
    public void Draw(Mesh3D mesh, Material material, in Matrix4x4 world, in RenderState3D state, in DassetBounds localBounds)
        => Draw(mesh.Geometry, material, world, state, localBounds);

    /// <summary>
    /// Uploads arbitrary instance data to a renderer-managed transient buffer
    /// and queues one instanced draw. The shader defines the instance layout.
    /// </summary>
    public void DrawInstances<TInstance>(
        Mesh mesh,
        Material material,
        ReadOnlySpan<TInstance> instances, Material? instancedDepthMaterial = null)
        where TInstance : unmanaged, IVertex
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        EnsureMeshDevice(mesh);
        if (instances.Length == 0)
            return;

        var buffer = GetInstanceBufferPool<TInstance>().Rent();
        buffer.Upload(instances);
        QueueInstances(mesh, material, buffer, instances.Length, instancedDepthMaterial);
    }

    /// <summary>
    /// Uploads arbitrary instance data to a renderer-managed transient buffer
    /// and queues one instanced draw. The shader defines the instance layout.
    /// </summary>
    public void DrawInstances<TInstance>(
        Mesh3D mesh,
        Material material,
        ReadOnlySpan<TInstance> instances, Material? instancedDepthMaterial = null)
        where TInstance : unmanaged, IVertex
        => DrawInstances(mesh.Geometry, material, instances, instancedDepthMaterial);

    /// <summary>
    /// Queues an instanced draw using a caller-owned instance buffer.
    /// </summary>
    public void DrawInstances(
        Mesh mesh,
        Material material,
        VertexBuffer instanceBuffer,
        int instanceCount, Material? instancedDepthMaterial = null)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        EnsureMeshDevice(mesh);
        ArgumentNullException.ThrowIfNull(instanceBuffer);
        if (!ReferenceEquals(instanceBuffer.GraphicsDevice, _graphicsDevice))
            throw new InvalidOperationException("Instance buffer and Renderer3D must belong to the same GraphicsDevice.");
        if (instanceBuffer.IsDisposed)
            throw new ObjectDisposedException(nameof(instanceBuffer));
        if (instanceCount < 0 || instanceCount > instanceBuffer.Count)
            throw new ArgumentOutOfRangeException(nameof(instanceCount));
        if (instanceCount == 0)
            return;

        QueueInstances(mesh, material, instanceBuffer, instanceCount, instancedDepthMaterial);
    }

    /// <summary>
    /// Queues an instanced draw using a caller-owned instance buffer.
    /// </summary>
    public void DrawInstances(
        Mesh3D mesh,
        Material material,
        VertexBuffer instanceBuffer,
        int instanceCount, Material? instancedDepthMaterial = null)
        => DrawInstances(mesh.Geometry, material, instanceBuffer, instanceCount, instancedDepthMaterial);

    private void QueueInstances(
        Mesh mesh,
        Material material,
        VertexBuffer instanceBuffer,
        int instanceCount, Material? instancedDepthMaterial = null)
    {
        // 实例化 draw 固定不透明；深度 shader 由调用方保证实例布局和动画一致。
        _items.Add(new DrawItem
        {
            Mesh = mesh,
            Material = material,
            State = RenderState3D.Opaque,
            InstanceBuffer = instanceBuffer,
            InstanceCount = instanceCount,
            InstancedDepthMaterial = instancedDepthMaterial,
            Sequence = _sequence++
        });
    }

    /// <summary>
    /// 稳定分队：把 <paramref name="items"/> 重排为 [不透明…][透明…]，两段各自保持原相对顺序，
    /// 返回透明队起始下标。抽成纯静态方法供单元测试（参照 Transform3DSystem.Run）。
    /// </summary>
    public static int PartitionByTransparency<T>(List<T> items, Func<T, bool> isTransparent)
    {
        var opaque = new List<T>(items.Count);
        var transparent = new List<T>();
        foreach (var item in items)
            (isTransparent(item) ? transparent : opaque).Add(item);

        items.Clear();
        items.AddRange(opaque);
        items.AddRange(transparent);
        return opaque.Count;
    }

    /// <summary>
    /// 透明队比较器：到相机距离平方大的（远的）排前面（back-to-front），同距按提交顺序，保证确定性。
    /// </summary>
    public static int CompareBackToFront(float leftDistanceSq, int leftSequence, float rightDistanceSq, int rightSequence)
    {
        var order = rightDistanceSq.CompareTo(leftDistanceSq);
        return order != 0 ? order : leftSequence.CompareTo(rightSequence);
    }

    /// <summary>
    /// Sorts and submits the collected draw requests: opaque queue first (grouped by
    /// material/mesh), then the transparent queue sorted back-to-front from the camera.
    /// </summary>
    public void End()
    {
        EnsureActive();

        try
        {
            var target = _target!;
            var camera = _camera!;
            camera.ViewportSize = new Point2(target.WidthInPixels, target.HeightInPixels);
            camera.Update();

            // 分队：不透明队维持原排序（减少状态切换），透明队按到相机距离从远到近（back-to-front）。
            var transparentStart = PartitionByTransparency(_items, static item => item.State.IsTransparent);
            _items.Sort(0, transparentStart, OpaqueDrawComparer.Instance);

            var cameraPosition = camera.Position;
            for (var i = transparentStart; i < _items.Count; i++)
                _items[i].DistanceSq = Vector3.DistanceSquared(_items[i].World.Translation, cameraPosition);
            _items.Sort(transparentStart, _items.Count - transparentStart, TransparentDrawComparer.Instance);

            var frustum = camera.GetFrustum();
            LastFrameCulledCount = 0;

            if (_shadowTarget != null && _shadowMaterial != null)
            {
                // 阴影 pass 只看不透明队：半透明写深度图会得到错误的实心影子，本期不进。
                for (var cascade = 0; cascade < (_cascades != null ? 4 : 1); cascade++)
                {
                    if (_cascades != null)
                    {
                        _shadowLightViewProjection = _cascades.Matrices[cascade];
                        _shadowViewport = _cascades.Viewport(cascade);
                    }
                    for (var i = 0; i < transparentStart; i++) SubmitShadow(_shadowTarget, _items[i]);
                }
            }

            foreach (var item in _items)
            {
                // 视锥剔除只作用于颜色 pass：屏外物体仍可能把阴影投进画面，阴影 pass 不剔。
                // 无 bounds 的 draw（实例化等）保守地总是提交。
                if (item.LocalBounds is { } localBounds)
                {
                    var worldBounds = localBounds.Transformed(item.World);
                    if (!frustum.IntersectsAabb(worldBounds.Min, worldBounds.Max))
                    {
                        LastFrameCulledCount++;
                        continue;
                    }
                }

                _lighting?.Apply(item.Material, camera);
                if (item.IsInstanced)
                    SubmitInstances(target, camera, item);
                else
                    SubmitDirect(target, camera, item);
            }
        }
        finally
        {
            _items.Clear();
            _target = null;
            _camera = null;
            _shadowTarget = null;
            _shadowMaterial = null;
            _skinnedShadowMaterial = null;
            _lighting = null;
            _cascades = null;
            _shadowViewport = null;
        }
    }

    private void SubmitDirect(IDrawableTarget target, Camera3D camera, DrawItem item)
    {
        item.Material.Vertex.SetUniformBuffer(new DirectVertexUniforms
        {
            WorldViewProjection = item.World * camera.ViewProjection,
            World = item.World,
        });

        if (item.JointPalette != null)
            item.Material.Vertex.SetUniformBuffer(MemoryMarshal.AsBytes(item.JointPalette.AsSpan()), 2);

        item.Mesh.GraphicsDevice.Draw(new DrawCommand(target, item.Mesh, item.Material)
        {
            BlendMode = item.State.Blend,
            CullMode = item.State.Cull,
            DepthCompare = DepthCompare.LessOrEqual,
            DepthTestEnabled = true,
            DepthWriteEnabled = item.State.DepthWrite,
        });
    }

    private void SubmitInstances(
        IDrawableTarget target,
        Camera3D camera,
        DrawItem item)
    {
        item.Material.Vertex.SetUniformBuffer(new InstancedVertexUniforms
        {
            ViewProjection = camera.ViewProjection,
        });

        var command = new DrawCommand(target, item.Mesh, item.Material);
        command.VertexBuffers.Add((item.InstanceBuffer!, true));
        command.InstanceCount = item.InstanceCount;
        command.BlendMode = item.State.Blend;
        command.CullMode = item.State.Cull;
        command.DepthCompare = DepthCompare.LessOrEqual;
        command.DepthTestEnabled = true;
        command.DepthWriteEnabled = item.State.DepthWrite;
        item.Mesh.GraphicsDevice.Draw(command);
    }

    /// <summary>
    /// 阴影深度 pass。面剔除翻转为 Front 消除自遮挡 acne；深度比较用 Less（深度图每帧清理）。
    /// 实例化 draw 仅使用调用方提供的配套深度变体，避免自定义实例动画与颜色 pass 不一致。
    /// 蒙皮 draw 仅在提供 skinnedDepthMaterial 时提交，并写入与颜色 pass 相同的 palette。
    /// </summary>
    private void SubmitShadow(IDrawableTarget shadowTarget, DrawItem item)
    {
        var material = item.IsInstanced ? item.InstancedDepthMaterial
            : item.JointPalette != null ? _skinnedShadowMaterial : _shadowMaterial;
        if (material == null)
            return;
        material.Vertex.SetUniformBuffer(new ShadowDirectVertexUniforms
        {
            WorldLightViewProjection = item.IsInstanced ? _shadowLightViewProjection : item.World * _shadowLightViewProjection,
        });

        if (item.JointPalette != null)
            material.Vertex.SetUniformBuffer(MemoryMarshal.AsBytes(item.JointPalette.AsSpan()), 2);

        var command = new DrawCommand(shadowTarget, item.Mesh, material)
        {
            CullMode = CullMode.Front,
            DepthCompare = DepthCompare.Less,
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
            Viewport = _shadowViewport,
        };
        if (item.IsInstanced)
        {
            command.VertexBuffers.Add((item.InstanceBuffer!, true));
            command.InstanceCount = item.InstanceCount;
        }
        _graphicsDevice.Draw(command);
    }

    private void EnsureActive()
    {
        if (_target == null || _camera == null)
            throw new InvalidOperationException("Renderer3D requires an active Begin/End pass.");
    }

    private void EnsureMeshDevice(Mesh mesh)
    {
        if (!ReferenceEquals(mesh.GraphicsDevice, _target!.GraphicsDevice))
            throw new InvalidOperationException("Mesh and render target must belong to the same GraphicsDevice.");
    }

    private InstanceBufferPool<TInstance> GetInstanceBufferPool<TInstance>()
        where TInstance : unmanaged, IVertex
    {
        var type = typeof(TInstance);
        if (!_instanceBufferPools.TryGetValue(type, out var untypedPool))
        {
            var newPool = new InstanceBufferPool<TInstance>(_graphicsDevice);
            _instanceBufferPools.Add(type, newPool);
            return newPool;
        }

        return (InstanceBufferPool<TInstance>)untypedPool;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _items.Clear();
        _target = null;
        _camera = null;
        foreach (var pool in _instanceBufferPools.Values)
            pool.Dispose();
        _instanceBufferPools.Clear();
    }
}
