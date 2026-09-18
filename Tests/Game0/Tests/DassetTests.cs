using System.Numerics;
using DragonLib.Gltf;
using Engine.Assets.Dasset;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// .dasset 烘焙管线的纯数据往返：GltfModelCooker cook .glb → DassetWriter/DassetReader
/// 内存流 round-trip。纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class DassetTests
{
    private static string CubeGlbPath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../Resources/Models/fbx_cube.glb"));

    [Fact]
    public void CookCubeProducesExpectedGeometry()
    {
        var model = GltfModelCooker.Cook(CubeGlbPath);

        var primitive = Assert.Single(model.Primitives);
        Assert.Equal(24, primitive.Vertices.Length);
        Assert.Equal(36, primitive.Indices.Length);
        Assert.Empty(model.Textures);

        // fbx_cube 是 1.2 见方的立方体，节点烘焙后位于 z ∈ [-1.2, 0]。
        Assert.Equal(new Vector3(-0.6f, -0.6f, -1.2f), primitive.Bounds.Min);
        Assert.Equal(new Vector3(0.6f, 0.6f, 0f), primitive.Bounds.Max);
        Assert.Equal(primitive.Bounds.Min, model.Bounds.Min);
        Assert.Equal(primitive.Bounds.Max, model.Bounds.Max);
    }

    [Fact]
    public void CookedCubeRoundTripsThroughWriterAndReader()
    {
        var cooked = GltfModelCooker.Cook(CubeGlbPath);

        using var stream = new MemoryStream();
        DassetWriter.Write(stream, cooked);
        stream.Position = 0;
        var read = DassetReader.Read(stream);

        Assert.Equal(cooked.Primitives.Count, read.Primitives.Count);
        var expected = cooked.Primitives[0];
        var actual = read.Primitives[0];
        Assert.Equal(expected.Vertices, actual.Vertices);
        Assert.Equal(expected.Indices, actual.Indices);
        Assert.Equal(expected.Bounds.Min, actual.Bounds.Min);
        Assert.Equal(expected.Bounds.Max, actual.Bounds.Max);
        Assert.Equal(cooked.Bounds.Min, read.Bounds.Min);
        Assert.Equal(cooked.Bounds.Max, read.Bounds.Max);

        Assert.Equal(expected.Material.BaseColorFactor, actual.Material.BaseColorFactor);
        Assert.Equal(expected.Material.Metallic, actual.Material.Metallic);
        Assert.Equal(expected.Material.Roughness, actual.Material.Roughness);
        Assert.Equal(expected.Material.DoubleSided, actual.Material.DoubleSided);
        Assert.Equal(expected.Material.AlphaMode, actual.Material.AlphaMode);
        Assert.Equal(expected.Material.AlphaCutoff, actual.Material.AlphaCutoff);
        Assert.Equal(expected.Material.AlbedoTextureIndex, actual.Material.AlbedoTextureIndex);
        Assert.Equal(expected.Material.NormalTextureIndex, actual.Material.NormalTextureIndex);
    }

    [Fact]
    public void MaterialAlphaModeSurvivesRoundTrip()
    {
        // 老 GltfModelLoader 丢 AlphaMode；cook 管线必须保住 Mask/Blend。
        var model = new DassetModel();
        model.Primitives.Add(new DassetPrimitive
        {
            Vertices = [new Engine.Rendering.PositionNormalUvVertex()],
            Indices = [0u, 0u, 0u],
            Material = new DassetMaterial
            {
                AlphaMode = DassetAlphaMode.Blend,
                AlphaCutoff = 0.25f,
                DoubleSided = true,
            },
        });

        using var stream = new MemoryStream();
        DassetWriter.Write(stream, model);
        stream.Position = 0;
        var read = DassetReader.Read(stream);

        Assert.Equal(DassetAlphaMode.Blend, read.Primitives[0].Material.AlphaMode);
        Assert.Equal(0.25f, read.Primitives[0].Material.AlphaCutoff);
        Assert.True(read.Primitives[0].Material.DoubleSided);
    }

    [Fact]
    public void TextureTableRoundTripsRawBytes()
    {
        var model = new DassetModel();
        model.Textures.Add(new DassetTextureEntry
        {
            Name = "albedo.png",
            Codec = DassetTextureCodec.Png,
            Bytes = [1, 2, 3, 250],
        });
        model.Primitives.Add(new DassetPrimitive
        {
            Vertices = [new Engine.Rendering.PositionNormalUvVertex()],
            Indices = [0u, 0u, 0u],
            Material = new DassetMaterial { AlbedoTextureIndex = 0 },
        });

        using var stream = new MemoryStream();
        DassetWriter.Write(stream, model);
        stream.Position = 0;
        var read = DassetReader.Read(stream);

        var texture = Assert.Single(read.Textures);
        Assert.Equal("albedo.png", texture.Name);
        Assert.Equal(DassetTextureCodec.Png, texture.Codec);
        Assert.Equal(model.Textures[0].Bytes, texture.Bytes);
        Assert.Equal(0, read.Primitives[0].Material.AlbedoTextureIndex);
    }

    [Fact]
    public void ReadRejectsBadMagic()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0xDEADBEEFu);
            writer.Write(DassetFormat.Version);
        }
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => DassetReader.Read(stream, "bad-magic"));
    }

    [Fact]
    public void ReadRejectsUnsupportedVersion()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(DassetFormat.Magic);
            writer.Write(DassetFormat.Version + 1);
        }
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => DassetReader.Read(stream, "bad-version"));
    }
}
