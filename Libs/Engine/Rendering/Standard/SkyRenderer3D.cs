using System.Numerics;
using System.Runtime.InteropServices;
using Engine.World;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>在 Clear 后、场景 draw 前绘制天空，不写深度，后续几何自然覆盖它。</summary>
public sealed class SkyRenderer3D : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SkyUniforms { public Matrix4x4 InverseViewProjection; public Vector4 CameraPosition, Settings; }
    private readonly Batcher _batch;
    private readonly EmbeddedShaderMaterial _shader;
    public SkyRenderer3D(GraphicsDevice device)
    {
        _batch = new Batcher(device);
        _shader = EmbeddedShaderMaterial.Load(device, typeof(SkyRenderer3D).Assembly, "Engine/Shaders/Sky3D",
            new ShaderStageSpec(1, 1, "fragment_main"), new ShaderStageSpec(0, 1, "vertex_main"));
    }
    public void Draw(RenderTarget3D target, Camera3D camera, EnvironmentMap3D environment, float intensity = 1, float rotation = 0)
    {
        ObjectDisposedException.ThrowIf(environment.IsDisposed, environment);
        camera.ViewportSize = new(target.Width, target.Height);
        if (!Matrix4x4.Invert(camera.ViewProjection, out var inverse)) throw new InvalidOperationException("Singular camera matrix.");
        _shader.Material.Fragment.SetUniformBuffer(new SkyUniforms
        {
            InverseViewProjection = inverse, CameraPosition = new Vector4(camera.Position, 1),
            Settings = new Vector4(MathF.Max(0, intensity), rotation, target.IsHdr ? 1 : 0, 0),
        });
        _batch.Clear();
        _batch.PushMaterial(_shader.Material);
        _batch.PushBlend(new BlendMode(BlendOp.Add, BlendFactor.One, BlendFactor.Zero));
        _batch.PushSampler(new TextureSampler(TextureFilter.Linear, TextureWrap.Repeat, TextureWrap.Clamp));
        try
        {
            _batch.ImageStretch(new Subtexture(environment.Sky), new Rect(0, 0, target.Width, target.Height), Color.White);
            _batch.Render(target.Target);
        }
        finally { _batch.PopSampler(); _batch.PopBlend(); _batch.PopMaterial(); _batch.Clear(); }
    }
    public void Dispose() { _shader.Dispose(); _batch.Dispose(); }
}
