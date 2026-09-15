using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        public VertexBuffer? InstanceBuffer;
        public int InstanceCount;
        public int Sequence;

        public bool IsInstanced => InstanceBuffer != null;
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
    private Matrix4x4 _shadowLightViewProjection;

    public bool IsActive => _target != null;

    public Renderer3D(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>
    /// 为本帧启用阴影 pass：End() 会先把已收集的 draw 提交到 <paramref name="shadowTarget"/>
    /// （用 <paramref name="depthMaterial"/>，面剔除翻转为 Front 消 acne），再提交颜色 pass。
    /// 须在 Begin 之后、End 之前调用。depthMaterial 的顶点 uniform 需为单个
    /// WorldLightViewProjection 矩阵（见 DepthOnly.hlsl）。
    /// 实例化 draw 不进阴影 pass。
    /// </summary>
    public void SetShadowPass(
        IDrawableTarget shadowTarget,
        Material depthMaterial,
        in Matrix4x4 lightViewProjection)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(shadowTarget);
        ArgumentNullException.ThrowIfNull(depthMaterial);
        if (!ReferenceEquals(shadowTarget.GraphicsDevice, _graphicsDevice))
            throw new InvalidOperationException("Shadow target and Renderer3D must belong to the same GraphicsDevice.");

        _shadowTarget = shadowTarget;
        _shadowMaterial = depthMaterial;
        _shadowLightViewProjection = lightViewProjection;
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
            Sequence = _sequence++
        });
    }

    /// <summary>
    /// Queues one mesh transform for a direct draw shader.
    /// </summary>
    public void Draw(Mesh3D mesh, Material material, in Matrix4x4 world)
        => Draw(mesh.Geometry, material, world);

    /// <summary>
    /// Uploads arbitrary instance data to a renderer-managed transient buffer
    /// and queues one instanced draw. The shader defines the instance layout.
    /// </summary>
    public void DrawInstances<TInstance>(
        Mesh mesh,
        Material material,
        ReadOnlySpan<TInstance> instances)
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
        QueueInstances(mesh, material, buffer, instances.Length);
    }

    /// <summary>
    /// Uploads arbitrary instance data to a renderer-managed transient buffer
    /// and queues one instanced draw. The shader defines the instance layout.
    /// </summary>
    public void DrawInstances<TInstance>(
        Mesh3D mesh,
        Material material,
        ReadOnlySpan<TInstance> instances)
        where TInstance : unmanaged, IVertex
        => DrawInstances(mesh.Geometry, material, instances);

    /// <summary>
    /// Queues an instanced draw using a caller-owned instance buffer.
    /// </summary>
    public void DrawInstances(
        Mesh mesh,
        Material material,
        VertexBuffer instanceBuffer,
        int instanceCount)
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

        QueueInstances(mesh, material, instanceBuffer, instanceCount);
    }

    /// <summary>
    /// Queues an instanced draw using a caller-owned instance buffer.
    /// </summary>
    public void DrawInstances(
        Mesh3D mesh,
        Material material,
        VertexBuffer instanceBuffer,
        int instanceCount)
        => DrawInstances(mesh.Geometry, material, instanceBuffer, instanceCount);

    private void QueueInstances(
        Mesh mesh,
        Material material,
        VertexBuffer instanceBuffer,
        int instanceCount)
    {
        _items.Add(new DrawItem
        {
            Mesh = mesh,
            Material = material,
            InstanceBuffer = instanceBuffer,
            InstanceCount = instanceCount,
            Sequence = _sequence++
        });
    }

    /// <summary>
    /// Sorts and submits the collected draw requests.
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

            _items.Sort(static (left, right) =>
            {
                var materialOrder = RuntimeHelpers.GetHashCode(left.Material)
                    .CompareTo(RuntimeHelpers.GetHashCode(right.Material));
                if (materialOrder != 0)
                    return materialOrder;

                var meshOrder = RuntimeHelpers.GetHashCode(left.Mesh)
                    .CompareTo(RuntimeHelpers.GetHashCode(right.Mesh));
                if (meshOrder != 0)
                    return meshOrder;

                var instanceOrder = left.IsInstanced.CompareTo(right.IsInstanced);
                return instanceOrder != 0 ? instanceOrder : left.Sequence.CompareTo(right.Sequence);
            });

            if (_shadowTarget != null && _shadowMaterial != null)
            {
                foreach (var item in _items)
                    SubmitShadow(_shadowTarget, item);
            }

            foreach (var item in _items)
            {
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
        }
    }

    private void SubmitDirect(IDrawableTarget target, Camera3D camera, DrawItem item)
    {
        item.Material.Vertex.SetUniformBuffer(new DirectVertexUniforms
        {
            WorldViewProjection = item.World * camera.ViewProjection,
            World = item.World,
        });

        item.Mesh.GraphicsDevice.Draw(new DrawCommand(target, item.Mesh, item.Material)
        {
            BlendMode = BlendMode.NonPremultiplied,
            CullMode = CullMode.Back,
            DepthCompare = DepthCompare.LessOrEqual,
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
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
        command.BlendMode = BlendMode.NonPremultiplied;
        command.CullMode = CullMode.Back;
        command.DepthCompare = DepthCompare.LessOrEqual;
        command.DepthTestEnabled = true;
        command.DepthWriteEnabled = true;
        item.Mesh.GraphicsDevice.Draw(command);
    }

    /// <summary>
    /// 阴影深度 pass。面剔除翻转为 Front 消除自遮挡 acne；深度比较用 Less（深度图每帧清理）。
    /// 实例化 draw 暂不进阴影 pass（DepthOnly 着色器不读实例缓冲，进来了也是错的）。
    /// </summary>
    private void SubmitShadow(IDrawableTarget shadowTarget, DrawItem item)
    {
        if (item.IsInstanced)
            return;

        _shadowMaterial!.Vertex.SetUniformBuffer(new ShadowDirectVertexUniforms
        {
            WorldLightViewProjection = item.World * _shadowLightViewProjection,
        });

        _graphicsDevice.Draw(new DrawCommand(shadowTarget, item.Mesh, _shadowMaterial)
        {
            CullMode = CullMode.Front,
            DepthCompare = DepthCompare.Less,
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
        });
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
