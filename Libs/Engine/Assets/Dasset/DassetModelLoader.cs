using Engine.Rendering;
using Foster.Framework;

namespace Engine.Assets.Dasset;

/// <summary>
/// 把 .dasset 加载成 <see cref="DassetModelAsset"/>：<see cref="DassetReader"/> 读回纯数据，
/// 逐贴图 Foster Image 解码并上传 GPU，逐 primitive 建 Mesh——GPU 上传只发生在这一层，
/// 解析本身是二进制直读，不经过 SharpGLTF。
/// </summary>
public static class DassetModelLoader
{
    /// <summary>从存储读取(桌面 LocalStorage、Web 预加载的 title storage 都可，见 <see cref="GameStorage"/>)。</summary>
    public static DassetModelAsset Load(GraphicsDevice device, StorageContainer storage, string path)
    {
        ArgumentNullException.ThrowIfNull(storage);
        using var stream = storage.OpenRead(path);
        return Load(device, DassetReader.Read(stream, path), Path.GetFileName(path));
    }

    public static DassetModelAsset Load(GraphicsDevice device, string filePath, string? assetName = null)
    {
        var model = DassetReader.Read(filePath);
        return Load(device, model, assetName ?? Path.GetFileName(filePath));
    }

    public static DassetModelAsset Load(GraphicsDevice device, DassetModel model, string assetName)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(model);

        var asset = new DassetModelAsset { Name = assetName };

        // 贴图表按下标原样上传，材质里的索引才能对齐。cook 端只收 PNG/JPG（Foster Image 能处理的格式）。
        var colorUses = new bool[model.Textures.Count];
        var dataUses = new bool[model.Textures.Count];
        foreach (var primitive in model.Primitives)
        {
            MarkTextureUse(colorUses, primitive.Material.AlbedoTextureIndex);
            MarkTextureUse(dataUses, primitive.Material.NormalTextureIndex);
        }
        for (var i = 0; i < model.Textures.Count; i++)
        {
            var entry = model.Textures[i];
            using var image = new Image(entry.Bytes);
            var srgb = colorUses[i] && device.IsTextureFormatSupported(TextureFormat.R8G8B8A8Srgb);
            var format = srgb && !dataUses[i] ? TextureFormat.R8G8B8A8Srgb : TextureFormat.Color;
            asset.AddTexture(UploadTexture(device, image, format, $"dasset:{assetName}:{entry.Name}"));
            if (srgb && dataUses[i])
                asset.AddColorTexture(i, UploadTexture(device, image, TextureFormat.R8G8B8A8Srgb, $"dasset:{assetName}:{entry.Name}:sRGB"));
        }

        foreach (var skeleton in model.Skeletons)
            asset.AddSkeleton(skeleton);
        foreach (var clip in model.Clips)
            asset.AddClip(clip);

        foreach (var primitive in model.Primitives)
        {
            if (primitive.SkinVertices is { } skinVertices)
            {
                var skinnedMesh = new Mesh<PositionNormalUvSkinVertex, uint>(device, $"dasset:{assetName}");
                skinnedMesh.SetVertices(skinVertices);
                skinnedMesh.SetIndices(primitive.Indices);
                asset.Add(new DassetMeshPrimitive { Mesh = skinnedMesh, Material = primitive.Material, Bounds = primitive.Bounds, SkinIndex = primitive.SkinIndex });
            }
            else
            {
                var mesh = new Mesh<PositionNormalUvVertex, uint>(device, $"dasset:{assetName}");
                mesh.SetVertices(primitive.Vertices);
                mesh.SetIndices(primitive.Indices);
                asset.Add(new DassetMeshPrimitive { Mesh = mesh, Material = primitive.Material, Bounds = primitive.Bounds });
            }
        }

        return asset;
    }

    private static void MarkTextureUse(bool[] uses, int index)
    {
        if (index >= 0 && index < uses.Length) uses[index] = true;
    }

    private static Texture UploadTexture(GraphicsDevice device, Image image, TextureFormat format, string name)
    {
        var texture = new Texture(device, image.Width, image.Height, format, TextureFlags.GenerateMipmaps, name);
        texture.SetData<Color>(image.Data);
        return texture;
    }
}
