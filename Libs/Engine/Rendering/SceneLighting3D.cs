using System.Numerics;
using System.Runtime.InteropServices;
using Engine.World;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>一帧的标准管线光照；Renderer3D 在提交颜色 draw 前写入，避免共享材质残留上一相机的数据。</summary>
public sealed class SceneLighting3D
{
    public Vector3 Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, 0.35f));
    public Vector3 DirectionalColor = Vector3.One;
    public Vector3 AmbientColor = new(0.35f);
    public List<PointLight3D> PointLights { get; } = [];
    public bool ShadowsEnabled;
    public float ShadowBias = 0.0015f;
    public float ShadowDarkness = 0.65f;
    public ShadowMap? ShadowMap;
    public bool HdrEnabled;
    private readonly float[] _packedLights = new float[PointLight3D.PackedFloatCount];

    public LightUniforms GetLightUniforms(Vector3 cameraPosition) => new()
    {
        LightDirection = new Vector4(Direction.LengthSquared() > 0f ? Vector3.Normalize(Direction) : -Vector3.UnitY, 0f),
        Ambient = new Vector4(AmbientColor, 1f),
        Diffuse = new Vector4(DirectionalColor, 1f),
        CameraPosition = new Vector4(cameraPosition, 1f),
        ColorPipeline = new Vector4(HdrEnabled ? 1f : 0f, 0f, 0f, 0f),
    };

    public ShadowSettingsUniforms GetShadowUniforms() => new()
    {
        Settings = new Vector4(ShadowsEnabled && ShadowMap != null ? 1f : 0f,
            ShadowMap != null ? 1f / ShadowMap.Target.Width : 0f, ShadowBias, ShadowDarkness),
    };

    public void Apply(Material material, Camera3D camera)
    {
        material.Fragment.SetUniformBuffer(GetLightUniforms(camera.Position));
        material.Fragment.SetUniformBuffer(GetShadowUniforms(), 2);
        PointLight3D.Pack(CollectionsMarshal.AsSpan(PointLights), _packedLights);
        material.Fragment.SetUniformBuffer(_packedLights.AsSpan(), 3);
        material.Vertex.SetUniformBuffer(new ShadowMatrixUniforms
        {
            LightViewProjection = ShadowMap?.LightViewProjection ?? Matrix4x4.Identity,
        }, 1);
        if (ShadowMap != null)
            material.Fragment.Samplers[2] = new BoundSampler(ShadowMap.DepthTexture,
                new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp));
    }
}
