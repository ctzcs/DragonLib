using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Engine.Rendering;

namespace Engine.Assets.Dasset;

/// <summary>
/// 把 <see cref="DassetModel"/> 按 <see cref="DassetFormat"/> 布局写成二进制流（v2）。
/// 顶点/索引是 unmanaged 数组，整块按字节写，不做逐字段序列化。
/// </summary>
public static class DassetWriter
{
    public static void Write(string filePath, DassetModel model)
    {
        using var stream = File.Create(filePath);
        Write(stream, model);
    }

    public static void Write(Stream stream, DassetModel model)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(model);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(DassetFormat.Magic);
        writer.Write(DassetFormat.Version);

        writer.Write(model.Textures.Count);
        foreach (var texture in model.Textures)
        {
            writer.Write(texture.Name);
            writer.Write((int)texture.Codec);
            writer.Write(texture.Bytes.Length);
            writer.Write(texture.Bytes);
        }

        writer.Write(model.Skeletons.Count);
        foreach (var skeleton in model.Skeletons)
        {
            writer.Write(skeleton.Joints.Count);
            foreach (var joint in skeleton.Joints)
            {
                writer.Write(joint.Name);
                writer.Write(joint.ParentIndex);
                Write(writer, joint.BindTranslation);
                Write(writer, joint.BindRotation);
                Write(writer, joint.BindScale);
                Write(writer, joint.InverseBindMatrix);
            }
        }

        writer.Write(model.Primitives.Count);
        foreach (var primitive in model.Primitives)
        {
            writer.Write((int)(primitive.IsSkinned ? DassetVertexLayout.PositionNormalUvSkin : DassetVertexLayout.PositionNormalUv));
            writer.Write(primitive.SkinIndex);

            if (primitive.SkinVertices is { } skinVertices)
            {
                writer.Write(skinVertices.Length);
                writer.Write(MemoryMarshal.AsBytes(skinVertices.AsSpan()));
            }
            else
            {
                writer.Write(primitive.Vertices.Length);
                writer.Write(MemoryMarshal.AsBytes(primitive.Vertices.AsSpan()));
            }

            writer.Write(primitive.Indices.Length);
            writer.Write(MemoryMarshal.AsBytes(primitive.Indices.AsSpan()));
            Write(writer, primitive.Bounds);
            Write(writer, primitive.Material);
        }

        Write(writer, model.Bounds);

        writer.Write(model.Clips.Count);
        foreach (var clip in model.Clips)
        {
            writer.Write(clip.Name);
            writer.Write(clip.SkinIndex);
            writer.Write(clip.Duration);
            writer.Write(clip.Channels.Count);
            foreach (var channel in clip.Channels)
            {
                writer.Write(channel.JointIndex);
                writer.Write((int)channel.Path);
                writer.Write(channel.Times.Length);
                foreach (var time in channel.Times)
                    writer.Write(time);
                foreach (var value in channel.Values)
                    Write(writer, value);
            }
        }
    }

    private static void Write(BinaryWriter writer, Vector3 v)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
        writer.Write(v.Z);
    }

    private static void Write(BinaryWriter writer, Vector4 v)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
        writer.Write(v.Z);
        writer.Write(v.W);
    }

    private static void Write(BinaryWriter writer, Quaternion q)
    {
        writer.Write(q.X);
        writer.Write(q.Y);
        writer.Write(q.Z);
        writer.Write(q.W);
    }

    private static void Write(BinaryWriter writer, Matrix4x4 m)
    {
        writer.Write(m.M11); writer.Write(m.M12); writer.Write(m.M13); writer.Write(m.M14);
        writer.Write(m.M21); writer.Write(m.M22); writer.Write(m.M23); writer.Write(m.M24);
        writer.Write(m.M31); writer.Write(m.M32); writer.Write(m.M33); writer.Write(m.M34);
        writer.Write(m.M41); writer.Write(m.M42); writer.Write(m.M43); writer.Write(m.M44);
    }

    private static void Write(BinaryWriter writer, DassetBounds bounds)
    {
        writer.Write(bounds.Min.X);
        writer.Write(bounds.Min.Y);
        writer.Write(bounds.Min.Z);
        writer.Write(bounds.Max.X);
        writer.Write(bounds.Max.Y);
        writer.Write(bounds.Max.Z);
    }

    private static void Write(BinaryWriter writer, DassetMaterial material)
    {
        writer.Write(material.BaseColorFactor.X);
        writer.Write(material.BaseColorFactor.Y);
        writer.Write(material.BaseColorFactor.Z);
        writer.Write(material.BaseColorFactor.W);
        writer.Write(material.Metallic);
        writer.Write(material.Roughness);
        writer.Write(material.DoubleSided);
        writer.Write((int)material.AlphaMode);
        writer.Write(material.AlphaCutoff);
        writer.Write(material.AlbedoTextureIndex);
        writer.Write(material.NormalTextureIndex);
        writer.Write(material.MetallicRoughnessTextureIndex);
        writer.Write(material.OcclusionTextureIndex);
        writer.Write(material.OcclusionStrength);
        writer.Write(material.EmissiveTextureIndex);
        Write(writer, material.EmissiveFactor);
        writer.Write(material.NormalScale);
    }
}
