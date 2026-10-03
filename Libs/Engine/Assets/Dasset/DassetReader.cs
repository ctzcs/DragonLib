using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Engine.Rendering;

namespace Engine.Assets.Dasset;

/// <summary>
/// 把 .dasset 二进制流读回 <see cref="DassetModel"/>。读时校验 magic/version，
/// 不符抛带路径信息的 <see cref="InvalidDataException"/>；计数与剩余长度做基本 sanity 检查，
/// 避免坏文件导致巨额分配。兼容 v1（无骨架/剪辑段，primitive 无布局前缀，顶点定长静态格式）。
/// </summary>
public static class DassetReader
{
    private static readonly int StaticVertexStride = System.Runtime.CompilerServices.Unsafe.SizeOf<PositionNormalUvVertex>();
    private static readonly int SkinVertexStride = System.Runtime.CompilerServices.Unsafe.SizeOf<PositionNormalUvSkinVertex>();

    public static DassetModel Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Read(stream, filePath);
    }

    public static DassetModel Read(Stream stream, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var source = sourceName ?? "<stream>";

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != DassetFormat.Magic)
            throw new InvalidDataException($"DassetReader: '{source}' 不是 .dasset 文件（magic 不符）。");
        var version = reader.ReadInt32();
        if (version is < 1 or > DassetFormat.Version)
            throw new InvalidDataException($"DassetReader: '{source}' 的 .dasset 版本是 {version}，当前支持 1~{DassetFormat.Version}。请重新 cook。");

        var model = new DassetModel();

        var textureCount = ReadCount(reader, stream, source, 1);
        for (var i = 0; i < textureCount; i++)
        {
            var name = reader.ReadString();
            var codec = (DassetTextureCodec)reader.ReadInt32();
            if (!Enum.IsDefined(codec))
                throw new InvalidDataException($"DassetReader: '{source}' 贴图 '{name}' 的编码值非法（{(int)codec}）。");
            var byteCount = ReadCount(reader, stream, source, 1);
            model.Textures.Add(new DassetTextureEntry
            {
                Name = name,
                Codec = codec,
                Bytes = reader.ReadBytes(byteCount),
            });
        }

        if (version >= 2)
        {
            var skeletonCount = ReadCount(reader, stream, source, 1);
            for (var i = 0; i < skeletonCount; i++)
            {
                var jointCount = ReadCount(reader, stream, source, 1);
                var skeleton = new DassetSkeleton();
                for (var j = 0; j < jointCount; j++)
                {
                    skeleton.Joints.Add(new DassetJoint
                    {
                        Name = reader.ReadString(),
                        ParentIndex = reader.ReadInt32(),
                        BindTranslation = ReadVector3(reader),
                        BindRotation = ReadQuaternion(reader),
                        BindScale = ReadVector3(reader),
                        InverseBindMatrix = ReadMatrix(reader),
                    });
                }
                model.Skeletons.Add(skeleton);
            }
        }

        var primitiveCount = ReadCount(reader, stream, source, 1);
        for (var i = 0; i < primitiveCount; i++)
        {
            var layout = DassetVertexLayout.PositionNormalUv;
            var skinIndex = -1;
            if (version >= 2)
            {
                layout = (DassetVertexLayout)reader.ReadInt32();
                if (!Enum.IsDefined(layout))
                    throw new InvalidDataException($"DassetReader: '{source}' primitive {i} 的顶点布局值非法（{(int)layout}）。");
                skinIndex = reader.ReadInt32();
            }

            DassetPrimitive primitive;
            var vertexCount = ReadCount(reader, stream, source, layout == DassetVertexLayout.PositionNormalUvSkin ? SkinVertexStride : StaticVertexStride);
            if (layout == DassetVertexLayout.PositionNormalUvSkin)
            {
                var skinVertices = new PositionNormalUvSkinVertex[vertexCount];
                stream.ReadExactly(MemoryMarshal.AsBytes(skinVertices.AsSpan()));
                primitive = new DassetPrimitive { SkinVertices = skinVertices, SkinIndex = skinIndex };
            }
            else
            {
                var vertices = new PositionNormalUvVertex[vertexCount];
                stream.ReadExactly(MemoryMarshal.AsBytes(vertices.AsSpan()));
                primitive = new DassetPrimitive { Vertices = vertices };
            }

            var indices = new uint[ReadCount(reader, stream, source, 4)];
            stream.ReadExactly(MemoryMarshal.AsBytes(indices.AsSpan()));

            primitive.Indices = indices;
            primitive.Bounds = ReadBounds(reader);
            primitive.Material = ReadMaterial(reader, source, version);
            model.Primitives.Add(primitive);
        }

        model.Bounds = ReadBounds(reader);

        if (version >= 2)
        {
            var clipCount = ReadCount(reader, stream, source, 1);
            for (var i = 0; i < clipCount; i++)
            {
                var clip = new DassetAnimationClip
                {
                    Name = reader.ReadString(),
                    SkinIndex = reader.ReadInt32(),
                    Duration = reader.ReadSingle(),
                };
                var channelCount = ReadCount(reader, stream, source, 1);
                for (var c = 0; c < channelCount; c++)
                {
                    var channel = new DassetAnimationChannel
                    {
                        JointIndex = reader.ReadInt32(),
                        Path = (DassetAnimPath)reader.ReadInt32(),
                    };
                    if (!Enum.IsDefined(channel.Path))
                        throw new InvalidDataException($"DassetReader: '{source}' 剪辑 '{clip.Name}' 的 channel 路径值非法。");
                    if (version >= 4) channel.Interpolation = (DassetInterpolation)reader.ReadInt32();
                    if (!Enum.IsDefined(channel.Interpolation)) throw new InvalidDataException($"DassetReader: '{source}' 插值类型非法。");
                    var keyCount = ReadCount(reader, stream, source, channel.Interpolation == DassetInterpolation.CubicSpline ? 52 : 20);
                    channel.Times = new float[keyCount];
                    for (var k = 0; k < keyCount; k++)
                        channel.Times[k] = reader.ReadSingle();
                    channel.Values = new Vector4[keyCount];
                    for (var k = 0; k < keyCount; k++)
                        channel.Values[k] = ReadVector4(reader);
                    if (channel.Interpolation == DassetInterpolation.CubicSpline)
                    {
                        channel.InTangents = new Vector4[keyCount]; channel.OutTangents = new Vector4[keyCount];
                        for (var k = 0; k < keyCount; k++) channel.InTangents[k] = ReadVector4(reader);
                        for (var k = 0; k < keyCount; k++) channel.OutTangents[k] = ReadVector4(reader);
                    }
                    clip.Channels.Add(channel);
                }
                model.Clips.Add(clip);
            }
        }

        return model;
    }

    /// <summary>读元素计数并校验：非负，且按 elementSize 换算的字节数不超过流的剩余长度（可寻址时）。</summary>
    private static int ReadCount(BinaryReader reader, Stream stream, string source, int elementSize)
    {
        var count = reader.ReadInt32();
        if (count < 0 || (stream.CanSeek && (long)count * elementSize > stream.Length - stream.Position))
            throw new InvalidDataException($"DassetReader: '{source}' 数据损坏（元素计数 {count} 越界）。");
        return count;
    }

    private static Vector3 ReadVector3(BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static Vector4 ReadVector4(BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static Quaternion ReadQuaternion(BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static Matrix4x4 ReadMatrix(BinaryReader reader)
        => new(
            reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
            reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
            reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
            reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private static DassetBounds ReadBounds(BinaryReader reader) => new()
    {
        Min = ReadVector3(reader),
        Max = ReadVector3(reader),
    };

    private static DassetMaterial ReadMaterial(BinaryReader reader, string source, int version)
    {
        var material = new DassetMaterial
        {
            BaseColorFactor = new Vector4(
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            Metallic = reader.ReadSingle(),
            Roughness = reader.ReadSingle(),
            DoubleSided = reader.ReadBoolean(),
            AlphaMode = (DassetAlphaMode)reader.ReadInt32(),
            AlphaCutoff = reader.ReadSingle(),
            AlbedoTextureIndex = reader.ReadInt32(),
            NormalTextureIndex = reader.ReadInt32(),
        };
        if (version >= 3)
        {
            material.MetallicRoughnessTextureIndex = reader.ReadInt32();
            material.OcclusionTextureIndex = reader.ReadInt32();
            material.OcclusionStrength = reader.ReadSingle();
            material.EmissiveTextureIndex = reader.ReadInt32();
            material.EmissiveFactor = ReadVector3(reader);
            material.NormalScale = reader.ReadSingle();
        }
        if (!Enum.IsDefined(material.AlphaMode))
            throw new InvalidDataException($"DassetReader: '{source}' 材质的 AlphaMode 值非法（{(int)material.AlphaMode}）。");
        return material;
    }
}
