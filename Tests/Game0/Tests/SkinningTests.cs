using System.Numerics;
using DragonLib.Gltf;
using Engine.Animation;
using Engine.Assets.Dasset;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// 蒙皮链路：SharpGLTF Toolkit 程序化造三节关节链 + 蒙皮柱 + "wave" 剪辑（无外部资产），
/// cook → v2 写读 round-trip → SkeletonAnimator 采样验证关节矩阵。纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class SkinningTests
{
    private static DassetModel CookSample()
        => GltfModelCooker.Cook(ProceduralSkinnedModel.Build(), "procedural_skin");

    /// <summary>走真实 IO 路径（SaveGLB → 临时文件 → Cook），覆盖 joints/weights 的编码归一化。</summary>
    private static DassetModel CookSampleViaFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"skinning_test_{Guid.NewGuid():N}.glb");
        try
        {
            ProceduralSkinnedModel.Build().SaveGLB(path);
            return GltfModelCooker.Cook(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CookExtractsSkeletonInTopologicalOrder()
    {
        var model = CookSample();

        var skeleton = Assert.Single(model.Skeletons);
        Assert.Equal(3, skeleton.Joints.Count);
        // 拓扑序：父先于子（root → mid → tip）。
        Assert.Equal(-1, skeleton.Joints[0].ParentIndex);
        Assert.Equal(0, skeleton.Joints[1].ParentIndex);
        Assert.Equal(1, skeleton.Joints[2].ParentIndex);
        Assert.Equal(Vector3.UnitY, skeleton.Joints[1].BindTranslation);
        Assert.Equal(Vector3.UnitY, skeleton.Joints[2].BindTranslation);
    }

    [Fact]
    public void CookExtractsWaveClipWithLinearChannels()
    {
        var model = CookSample();

        var clip = Assert.Single(model.Clips);
        Assert.Equal(ProceduralSkinnedModel.ClipName, clip.Name);
        Assert.InRange(clip.Duration, 0.99f, 1.01f);
        Assert.Equal(0, clip.SkinIndex);
        Assert.Equal(2, clip.Channels.Count);
        foreach (var channel in clip.Channels)
        {
            Assert.Equal(DassetAnimPath.Rotation, channel.Path);
            Assert.Equal(3, channel.Times.Length);
            Assert.Equal(0f, channel.Times[0]);
            Assert.True(MathF.Abs(channel.Times[1] - 0.5f) < 1e-3f);
            Assert.True(MathF.Abs(channel.Times[2] - 1f) < 1e-3f);
        }
    }

    [Fact]
    public void SkinnedPrimitiveKeepsBindSpaceVerticesAndNormalizedWeights()
    {
        var model = CookSampleViaFile();

        var primitive = Assert.Single(model.Primitives);
        Assert.True(primitive.IsSkinned);
        Assert.Equal(0, primitive.SkinIndex);
        Assert.Empty(primitive.Vertices);

        var vertices = primitive.SkinVertices!;
        Assert.NotEmpty(vertices);
        // 顶点保持 mesh bind 空间（未烘焙节点矩阵）：y ∈ [0, 3]。
        Assert.All(vertices, v => Assert.InRange(v.Position.Y, -0.01f, 3.01f));
        // 权重归一化。
        Assert.All(vertices, v => Assert.True(
            MathF.Abs(v.Weights.X + v.Weights.Y + v.Weights.Z + v.Weights.W - 1f) < 1e-3f,
            $"权重未归一化: {v.Weights}"));
        // 底部环（y=0）绑定关节 0，顶部环（y=3）绑定关节 2（JOINTS_0 已按拓扑序重映射打包）。
        var bottom = vertices.Where(v => v.Position.Y < 0.01f).First();
        Assert.Equal(0u, bottom.Joints & 0xFFu);
        Assert.Equal(Vector4.UnitX, bottom.Weights);
        var top = vertices.Where(v => v.Position.Y > 2.99f).First();
        Assert.Equal(2u, top.Joints & 0xFFu);
    }

    [Fact]
    public void BindPosePaletteIsIdentityAtOriginAttachment()
    {
        var model = CookSample();
        var skeleton = model.Skeletons[0];

        // 骨架挂点在原点（root 的 bind 变换是恒等）：bind pose 的 palette 应全是恒等矩阵。
        var palette = new Matrix4x4[skeleton.Joints.Count];
        SkeletonAnimator.ComputePalette(skeleton, null, 0f, palette);

        foreach (var matrix in palette)
            AssertMatrixApprox(Matrix4x4.Identity, matrix, 1e-4f);
    }

    [Fact]
    public void WaveClipAtHalfTimeBendsTipToTheSide()
    {
        var model = CookSample();
        var skeleton = model.Skeletons[0];
        var clip = model.Clips[0];

        var palette = new Matrix4x4[skeleton.Joints.Count];
        SkeletonAnimator.ComputePalette(skeleton, clip, 0.5f, palette);

        // root 不动画：palette[0] 仍是恒等。
        AssertMatrixApprox(Matrix4x4.Identity, palette[0], 1e-4f);

        // 顶端顶点（0.25, 3, 0.25）绑定 tip：t=0.5 时 mid 摆 0.6rad + tip 反摆 0.4rad。
        // 手算期望 ≈ (-0.52, 2.86, 0.25)（推导见实现注释；这里验证弯曲方向与大致位置）。
        var skinned = Vector3.Transform(new Vector3(0.25f, 3f, 0.25f), palette[2]);
        Assert.True(skinned.X < -0.3f, $"期望顶点向左弯（x<0），实际 {skinned}");
        Assert.True(MathF.Abs(skinned.Y - 2.86f) < 0.1f, $"期望高度 ≈2.86，实际 {skinned}");
        Assert.True(MathF.Abs(skinned.Z - 0.25f) < 0.05f, $"z 不应被绕 Z 旋转改变，实际 {skinned}");
    }

    [Fact]
    public void AdvanceTimeWrapsLoopsAndClamps()
    {
        // 循环：超出回绕到 [0, duration)。
        Assert.True(MathF.Abs(SkeletonAnimator.AdvanceTime(0.8f, 0.5f, 1f, true, 1f) - 0.3f) < 1e-5f);
        // 负速度：从 0 倒退回绕到末尾附近。
        Assert.True(MathF.Abs(SkeletonAnimator.AdvanceTime(0.1f, 0.3f, -1f, true, 1f) - 0.8f) < 1e-5f);
        // 非循环：钳到末尾停住。
        Assert.Equal(1f, SkeletonAnimator.AdvanceTime(0.8f, 0.5f, 1f, false, 1f));
        // 时长为 0（无剪辑）：时间原样累加，不做回绕。
        Assert.Equal(1.3f, SkeletonAnimator.AdvanceTime(0.8f, 0.5f, 1f, true, 0f));
    }

    [Fact]
    public void ChannelSamplingInterpolatesAndClamps()
    {
        var channel = new DassetAnimationChannel
        {
            Path = DassetAnimPath.Translation,
            Times = [0f, 1f],
            Values = [Vector4.Zero, new Vector4(10f, 0f, 0f, 0f)],
        };

        Assert.Equal(Vector4.Zero, SkeletonAnimator.SampleChannel(channel, -1f));
        Assert.Equal(new Vector4(10f, 0f, 0f, 0f), SkeletonAnimator.SampleChannel(channel, 99f));
        Assert.Equal(new Vector4(5f, 0f, 0f, 0f), SkeletonAnimator.SampleChannel(channel, 0.5f));
    }

    [Fact]
    public void SkinningRoundTripsThroughWriterAndReader()
    {
        var cooked = CookSample();

        using var stream = new MemoryStream();
        DassetWriter.Write(stream, cooked);
        stream.Position = 0;
        var read = DassetReader.Read(stream);

        var skeleton = Assert.Single(read.Skeletons);
        var expectedSkeleton = cooked.Skeletons[0];
        Assert.Equal(expectedSkeleton.Joints.Count, skeleton.Joints.Count);
        for (var i = 0; i < skeleton.Joints.Count; i++)
        {
            Assert.Equal(expectedSkeleton.Joints[i].Name, skeleton.Joints[i].Name);
            Assert.Equal(expectedSkeleton.Joints[i].ParentIndex, skeleton.Joints[i].ParentIndex);
            Assert.Equal(expectedSkeleton.Joints[i].BindTranslation, skeleton.Joints[i].BindTranslation);
            Assert.Equal(expectedSkeleton.Joints[i].BindRotation, skeleton.Joints[i].BindRotation);
            Assert.Equal(expectedSkeleton.Joints[i].InverseBindMatrix, skeleton.Joints[i].InverseBindMatrix);
        }

        var primitive = Assert.Single(read.Primitives);
        Assert.True(primitive.IsSkinned);
        Assert.Equal(cooked.Primitives[0].SkinVertices!.Length, primitive.SkinVertices!.Length);
        Assert.Equal(cooked.Primitives[0].SkinVertices, primitive.SkinVertices);
        Assert.Equal(cooked.Primitives[0].Indices, primitive.Indices);

        var clip = Assert.Single(read.Clips);
        Assert.Equal(cooked.Clips[0].Name, clip.Name);
        Assert.Equal(cooked.Clips[0].Duration, clip.Duration);
        Assert.Equal(cooked.Clips[0].Channels.Count, clip.Channels.Count);
        Assert.Equal(cooked.Clips[0].Channels[0].Times, clip.Channels[0].Times);
        Assert.Equal(cooked.Clips[0].Channels[0].Values, clip.Channels[0].Values);
    }

    [Fact]
    public void ReaderStillReadsV1Files()
    {
        // 手写 v1 布局（无骨架/剪辑段，primitive 无布局前缀，顶点定长 48B）。
        var model = new DassetModel();
        model.Primitives.Add(new DassetPrimitive
        {
            Vertices = [new Engine.Rendering.PositionNormalUvVertex(new Vector3(1f, 2f, 3f), Vector3.UnitY, Vector2.Zero, Vector4.UnitX)],
            Indices = [0u, 0u, 0u],
            Bounds = new DassetBounds { Min = Vector3.One, Max = new Vector3(2f) },
        });

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(DassetFormat.Magic);
            writer.Write(1); // v1
            writer.Write(0); // textures
            writer.Write(1); // primitives
            var primitive = model.Primitives[0];
            writer.Write(primitive.Vertices.Length);
            writer.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(primitive.Vertices.AsSpan()));
            writer.Write(primitive.Indices.Length);
            writer.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(primitive.Indices.AsSpan()));
            writer.Write(primitive.Bounds.Min.X); writer.Write(primitive.Bounds.Min.Y); writer.Write(primitive.Bounds.Min.Z);
            writer.Write(primitive.Bounds.Max.X); writer.Write(primitive.Bounds.Max.Y); writer.Write(primitive.Bounds.Max.Z);
            // material（v1 与 v2 同布局）
            writer.Write(1f); writer.Write(1f); writer.Write(1f); writer.Write(1f);
            writer.Write(0f); writer.Write(1f); writer.Write(false);
            writer.Write(0); writer.Write(0.5f); writer.Write(-1); writer.Write(-1);
            // model bounds
            writer.Write(1f); writer.Write(1f); writer.Write(1f);
            writer.Write(2f); writer.Write(2f); writer.Write(2f);
        }
        stream.Position = 0;

        var read = DassetReader.Read(stream);

        var readPrimitive = Assert.Single(read.Primitives);
        Assert.False(readPrimitive.IsSkinned);
        Assert.Equal(new Vector3(1f, 2f, 3f), readPrimitive.Vertices[0].Position);
        Assert.Empty(read.Skeletons);
        Assert.Empty(read.Clips);
        Assert.Equal(new Vector3(2f), read.Bounds.Max);
    }

    private static void AssertMatrixApprox(Matrix4x4 expected, Matrix4x4 actual, float tolerance)
    {
        var e = ToArray(expected);
        var a = ToArray(actual);
        for (var i = 0; i < 16; i++)
            Assert.True(MathF.Abs(e[i] - a[i]) < tolerance,
                $"矩阵元素 [{i}] 期望 {e[i]}，实际 {a[i]}。\n期望:\n{expected}\n实际:\n{actual}");
    }

    private static float[] ToArray(Matrix4x4 m)
        => [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
}
