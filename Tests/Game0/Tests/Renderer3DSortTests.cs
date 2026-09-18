using Engine.Assets.Dasset;
using Engine.Rendering;
using Foster.Framework;
using Xunit;

namespace Game0.EntitiesTests;

/// <summary>
/// Renderer3D 的分队/排序静态逻辑 + DassetMaterial → RenderState3D 映射。
/// 纯 CPU 逻辑，不需要 GraphicsDevice。
/// </summary>
public sealed class Renderer3DSortTests
{
    [Fact]
    public void PartitionSplitsQueuesAndKeepsRelativeOrder()
    {
        var items = new List<(string Name, bool Transparent)>
        {
            ("opaqueA", false),
            ("glassA", true),
            ("opaqueB", false),
            ("glassB", true),
            ("opaqueC", false),
        };

        var transparentStart = Renderer3D.PartitionByTransparency(items, static item => item.Transparent);

        Assert.Equal(3, transparentStart);
        Assert.Equal(
            ["opaqueA", "opaqueB", "opaqueC", "glassA", "glassB"],
            items.Select(static item => item.Name).ToArray());
    }

    [Fact]
    public void PartitionWithNoTransparentItemsReturnsFullLength()
    {
        var items = new List<int> { 0, 1, 2 };

        var transparentStart = Renderer3D.PartitionByTransparency(items, static _ => false);

        Assert.Equal(items.Count, transparentStart);
        Assert.Equal([0, 1, 2], items);
    }

    [Fact]
    public void BackToFrontOrdersFarToNear()
    {
        var draws = new List<(float DistanceSq, int Sequence)>
        {
            (4f, 0),
            (25f, 1),
            (9f, 2),
        };

        draws.Sort(static (left, right) => Renderer3D.CompareBackToFront(
            left.DistanceSq, left.Sequence, right.DistanceSq, right.Sequence));

        Assert.Equal([(25f, 1), (9f, 2), (4f, 0)], draws);
    }

    [Fact]
    public void BackToFrontTieBreaksBySubmissionOrder()
    {
        var draws = new List<(float DistanceSq, int Sequence)>
        {
            (9f, 2),
            (9f, 0),
            (9f, 1),
        };

        draws.Sort(static (left, right) => Renderer3D.CompareBackToFront(
            left.DistanceSq, left.Sequence, right.DistanceSq, right.Sequence));

        Assert.Equal([(9f, 0), (9f, 1), (9f, 2)], draws);
    }

    [Fact]
    public void OpaqueAndMaskStayInOpaqueQueue()
    {
        var opaque = new DassetMaterial { AlphaMode = DassetAlphaMode.Opaque }.ToRenderState();
        Assert.False(opaque.IsTransparent);
        Assert.True(opaque.DepthWrite);
        Assert.Equal(CullMode.Back, opaque.Cull);

        // Mask 留在不透明队列：cutout 由 shader clip 处理，深度照常写入。
        var mask = new DassetMaterial { AlphaMode = DassetAlphaMode.Mask }.ToRenderState();
        Assert.False(mask.IsTransparent);
        Assert.True(mask.DepthWrite);
    }

    [Fact]
    public void BlendGoesToTransparentQueueWithoutDepthWrite()
    {
        var blend = new DassetMaterial { AlphaMode = DassetAlphaMode.Blend }.ToRenderState();
        Assert.True(blend.IsTransparent);
        Assert.False(blend.DepthWrite);
        Assert.Equal(BlendMode.NonPremultiplied, blend.Blend);
    }

    [Fact]
    public void DoubleSidedDisablesCullingInBothQueues()
    {
        var opaque = new DassetMaterial { AlphaMode = DassetAlphaMode.Opaque, DoubleSided = true }.ToRenderState();
        Assert.Equal(CullMode.None, opaque.Cull);
        Assert.False(opaque.IsTransparent);

        var blend = new DassetMaterial { AlphaMode = DassetAlphaMode.Blend, DoubleSided = true }.ToRenderState();
        Assert.Equal(CullMode.None, blend.Cull);
        Assert.True(blend.IsTransparent);
    }
}
