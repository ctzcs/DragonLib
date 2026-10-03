using System.Numerics;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>
/// 方向光的 shadow map：一个 [Color, Depth] 的离屏 Target（Foster 要求至少一个颜色附件，
/// 颜色通道不使用），深度 pass 由 Renderer3D.SetShadowPass 写入，主 pass 用 Standard3D
/// 采样 DepthTexture 做 PCF。深度格式可配（默认 Depth32，不支持时退 Depth16）。
/// </summary>
public sealed class ShadowMap : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly TextureFormat _depthFormat;
    private Target? _target;

    public ShadowMap(GraphicsDevice graphicsDevice, int size = 2048, TextureFormat depthFormat = TextureFormat.Depth32)
    {
        _graphicsDevice = graphicsDevice;
        // 深度精度不足时 PCF 会出现条纹 acne；不支持所选格式时退回全平台可用的 Depth16。
        _depthFormat = graphicsDevice.IsTextureFormatSupported(depthFormat) ? depthFormat : TextureFormat.Depth16;
        Size = size;
    }

    public int Size { get; }

    public Target Target => _target ?? throw new InvalidOperationException("The shadow map has not been created. Call Resize first.");

    /// <summary>深度附件；在深度 pass 结束后可作为贴图绑定到主 pass 的 shader。</summary>
    public Texture DepthTexture => Target.Attachments[1];

    /// <summary>光源的正交 ViewProjection（行向量约定，与 Camera3D 一致）。</summary>
    public Matrix4x4 LightViewProjection { get; private set; } = Matrix4x4.Identity;

    public void Resize(int size)
    {
        size = Math.Max(1, size);
        if (_target is { Width: var w, Height: var h } && w == size && h == size)
            return;

        _target?.Dispose();
        _target = new Target(
            _graphicsDevice,
            size,
            size,
            [TextureFormat.Color, _depthFormat],
            name: "Engine Shadow Map");
    }

    /// <summary>
    /// 按光照方向与场景包围球计算正交光矩阵。direction 指向光传播的方向（向下）。
    /// </summary>
    public void UpdateLight(Vector3 direction, Vector3 sceneCenter, float sceneRadius)
    {
        sceneRadius = MathF.Max(0.1f, sceneRadius);
        var dir = direction.LengthSquared() > 1e-8f ? Vector3.Normalize(direction) : -Vector3.UnitY;

        // up 不能与视线平行；光斜照时用 UnitY，正俯视时退到 UnitZ。
        var up = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        var eye = sceneCenter - dir * (sceneRadius * 2f);

        var view = Matrix4x4.CreateLookAt(eye, sceneCenter, up);
        var projection = Matrix4x4.CreateOrthographic(sceneRadius * 2f, sceneRadius * 2f, 0.1f, sceneRadius * 4f);
        LightViewProjection = view * projection;
    }

    /// <summary>只清深度（颜色通道不使用，无需清理）。</summary>
    public void Clear()
        => Target.Clear(default(Color), 1f, 0, ClearMask.Depth);

    public void Dispose()
    {
        _target?.Dispose();
        _target = null;
    }
}
