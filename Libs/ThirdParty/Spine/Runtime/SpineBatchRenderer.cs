using System.Numerics;
using Foster.Framework;
using Spine;
using FosterBlendMode = Foster.Framework.BlendMode;

namespace DragonLib.Spine;

/// <summary>
/// Renders Spine region and mesh attachments into a Foster Batcher, including
/// polygon clipping through the official Spine SkeletonClipping helper.
/// Call after <see cref="SpineSkeleton.Update"/> and before the batcher is rendered.
/// </summary>
public sealed class SpineBatchRenderer
{
    private float[] _worldVertices = [];
    private readonly SkeletonClipping _clipping = new();

    public void Draw(Batcher batcher, SpineSkeleton instance, Color? tint = null)
    {
        ArgumentNullException.ThrowIfNull(batcher);
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.Scale.X == 0f || instance.Scale.Y == 0f)
            return;

        var skeleton = instance.Skeleton;
        var drawOrder = skeleton.DrawOrder.AppliedPose;
        var rootTint = tint ?? Color.White;

        batcher.PushMatrix(instance.WorldMatrix);
        try
        {
            for (var i = 0; i < drawOrder.Count; i++)
            {
                var slot = drawOrder.Items[i];
                if (!slot.Bone.Active)
                    continue;

                if (_clipping.IsClipping)
                    _clipping.ClipEnd(slot);

                var attachment = slot.AppliedPose.Attachment;
                if (attachment is ClippingAttachment clippingAttachment)
                {
                    _clipping.ClipStart(skeleton, slot, clippingAttachment);
                    continue;
                }

                batcher.PushBlend(MapBlend(slot.Data.BlendMode));
                try
                {
                    if (attachment is RegionAttachment region)
                        DrawRegion(batcher, slot, region, rootTint);
                    else if (attachment is MeshAttachment mesh)
                        DrawMesh(batcher, skeleton, slot, mesh, rootTint);
                }
                finally
                {
                    batcher.PopBlend();
                }
            }
        }
        finally
        {
            _clipping.ClipEnd();
            batcher.PopMatrix();
        }
    }

    private void DrawRegion(Batcher batcher, Slot slot, RegionAttachment attachment, Color tint)
    {
        var sequence = attachment.Sequence;
        var index = sequence.ResolveIndex(slot.AppliedPose);
        var region = sequence.GetRegion(index) as AtlasRegion;
        var texture = region?.page.rendererObject as Texture;
        if (region == null || texture == null)
            return;

        var offsets = sequence.GetOffsets(index);
        var uvs = sequence.GetUVs(index);
        EnsureScratch(8);
        attachment.ComputeWorldVertices(slot, offsets, _worldVertices, 0, 2);

        var color = MultiplyColor(tint, attachment.GetColor(), slot.AppliedPose.GetColor());
        Span<int> triangles = stackalloc int[] { 0, 1, 2, 2, 3, 0 };
        DrawTriangles(batcher, texture, _worldVertices, triangles, uvs, color);
    }

    private void DrawMesh(Batcher batcher, Skeleton skeleton, Slot slot, MeshAttachment attachment, Color tint)
    {
        var sequence = attachment.Sequence;
        var index = sequence.ResolveIndex(slot.AppliedPose);
        var region = sequence.GetRegion(index) as AtlasRegion;
        var texture = region?.page.rendererObject as Texture;
        var triangles = attachment.Triangles;
        if (region == null || texture == null || triangles == null || triangles.Length < 3)
            return;

        EnsureScratch(attachment.WorldVerticesLength);
        attachment.ComputeWorldVertices(skeleton, slot, 0, attachment.WorldVerticesLength, _worldVertices, 0, 2);
        var uvs = sequence.GetUVs(index);
        var color = MultiplyColor(tint, attachment.GetColor(), slot.AppliedPose.GetColor());

        DrawTriangles(batcher, texture, _worldVertices, triangles, uvs, color);
    }

    private void DrawTriangles(
        Batcher batcher,
        Texture texture,
        float[] vertices,
        ReadOnlySpan<int> triangles,
        float[] uvs,
        Color color)
    {
        if (_clipping.IsClipping)
        {
            var sourceTriangles = triangles.ToArray();
            _clipping.ClipTriangles(vertices, sourceTriangles, sourceTriangles.Length, uvs);
            vertices = _clipping.ClippedVertices.Items;
            uvs = _clipping.ClippedUVs.Items;
            triangles = _clipping.ClippedTriangles.Items.AsSpan(0, _clipping.ClippedTriangles.Count);
        }

        for (var i = 0; i + 2 < triangles.Length; i += 3)
        {
            var a = triangles[i] * 2;
            var b = triangles[i + 1] * 2;
            var c = triangles[i + 2] * 2;
            batcher.Triangle(
                texture,
                Point(vertices, a),
                Point(vertices, b),
                Point(vertices, c),
                Uv(uvs, a), Uv(uvs, b), Uv(uvs, c), color);
        }
    }

    private void EnsureScratch(int worldLength)
    {
        if (_worldVertices.Length < worldLength)
            Array.Resize(ref _worldVertices, worldLength);
    }

    private static Vector2 Point(float[] values, int index)
        => new(values[index], values[index + 1]);

    private static Vector2 Uv(float[] values, int index)
        => new(values[index], values[index + 1]);

    private static Foster.Framework.BlendMode MapBlend(global::Spine.BlendMode blend)
        => blend switch
        {
            global::Spine.BlendMode.Additive => new(BlendOp.Add, BlendFactor.One, BlendFactor.One),
            global::Spine.BlendMode.Multiply => FosterBlendMode.Multiply,
            global::Spine.BlendMode.Screen => FosterBlendMode.Screen,
            _ => FosterBlendMode.Premultiply,
        };

    private static Color MultiplyColor(Color tint, global::Spine.Color32F attachment, global::Spine.Color32F slot)
    {
        var r = tint.R / 255f * attachment.r * slot.r;
        var g = tint.G / 255f * attachment.g * slot.g;
        var b = tint.B / 255f * attachment.b * slot.b;
        var a = tint.A / 255f * attachment.a * slot.a;
        return new Color((byte)(Math.Clamp(r, 0f, 1f) * 255f), (byte)(Math.Clamp(g, 0f, 1f) * 255f),
            (byte)(Math.Clamp(b, 0f, 1f) * 255f), (byte)(Math.Clamp(a, 0f, 1f) * 255f));
    }
}
