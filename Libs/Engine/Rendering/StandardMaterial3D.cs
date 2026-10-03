using System.Numerics;
using Engine.Assets.Dasset;
using Foster.Framework;

namespace Engine.Rendering;

public static class StandardMaterial3D
{
    public static MaterialUniforms Pack(DassetMaterial data, bool hasAlbedo, bool hasNormal) => new()
    {
        BaseColorFactor = data.BaseColorFactor,
        Flags = new Vector4(hasAlbedo ? 1f : 0f, hasNormal ? 1f : 0f, 1f, (float)data.AlphaMode),
        AlphaParams = new Vector4(data.AlphaCutoff, 0f, 0f, 0f),
        PbrParams = new Vector4(data.Metallic, data.Roughness, 0f, 0f),
    };

    public static (Material Material, RenderState3D State) Create(Standard3DShaders shaders,
        DassetMaterial data, IReadOnlyList<Texture> textures, bool skinned = false)
    {
        var material = (skinned ? shaders.Skinned : shaders.Standard).Clone();
        var albedo = Resolve(textures, data.AlbedoTextureIndex);
        var normal = Resolve(textures, data.NormalTextureIndex);
        var sampler = new TextureSampler(TextureFilter.Linear, TextureWrap.Repeat);
        material.Fragment.Samplers[0] = new BoundSampler(albedo ?? shaders.WhiteTexture, sampler);
        material.Fragment.Samplers[1] = new BoundSampler(normal ?? shaders.WhiteTexture, sampler);
        material.Fragment.Samplers[2] = new BoundSampler(shaders.WhiteTexture,
            new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp));
        material.Fragment.SetUniformBuffer(Pack(data, albedo != null, normal != null), 1);
        return (material, data.ToRenderState());
    }

    private static Texture? Resolve(IReadOnlyList<Texture> textures, int index)
        => index >= 0 && index < textures.Count ? textures[index] : null;
}

/// <summary>按模型对象缓存，避免同名模型热重载后继续引用已释放的贴图；卸载模型时调用 Clear。</summary>
public sealed class MaterialCache
{
    private readonly Dictionary<DassetModelAsset, IReadOnlyList<(Material, RenderState3D)>> _models = [];
    private Standard3DShaders? _shaders;

    public IReadOnlyList<(Material Material, RenderState3D State)> Get(DassetModelAsset model, Standard3DShaders shaders)
    {
        if (!ReferenceEquals(_shaders, shaders))
        {
            Clear();
            _shaders = shaders;
        }
        if (_models.TryGetValue(model, out var cached))
            return cached;
        var materials = new (Material, RenderState3D)[model.Primitives.Count];
        for (var i = 0; i < materials.Length; i++)
        {
            var primitive = model.Primitives[i];
            materials[i] = StandardMaterial3D.Create(shaders, primitive.Material, model.Textures, primitive.IsSkinned);
        }
        _models.Add(model, materials);
        return materials;
    }

    public void Clear() => _models.Clear();
}
