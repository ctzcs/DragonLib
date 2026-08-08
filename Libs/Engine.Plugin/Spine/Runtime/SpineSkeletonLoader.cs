using System.Text;
using Spine;

namespace Engine.Spine;

/// <summary>Loads JSON or binary Spine data using a Foster atlas.</summary>
public static class SpineSkeletonLoader
{
    public static SkeletonData LoadJson(Stream json, Atlas atlas, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(atlas);
        using var reader = new StreamReader(json, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        var loader = new SkeletonJson(atlas) { Scale = scale };
        return loader.ReadSkeletonData(reader);
    }

    public static SkeletonData LoadBinary(Stream binary, Atlas atlas, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(binary);
        ArgumentNullException.ThrowIfNull(atlas);
        var loader = new SkeletonBinary(atlas) { Scale = scale };
        return loader.ReadSkeletonData(binary);
    }
}
