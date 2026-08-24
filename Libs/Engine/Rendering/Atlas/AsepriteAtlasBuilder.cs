using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Foster.Framework;

namespace Engine;

/// <summary>打包区域：Source = 图集内的（已裁剪）像素矩形；Frame = 裁剪内容在未裁剪帧内的位置（X/Y 为负的左/上留白）与原始帧尺寸。</summary>
public readonly record struct AtlasRegionPlacement(RectInt Source, RectInt Frame);

/// <summary>一条自动生成的动画：controller 来自 .aseprite 文件名，clip 来自 frame tag。</summary>
public readonly record struct BuiltClip(string Controller, string Name, string[] Frames, float[] Durations, bool Loop);

/// <summary>打包产物：合成后的图集像素 + 区域表 + 自动生成的动画剪辑。</summary>
public sealed class BuiltAtlas : IDisposable
{
    public required Image Image { get; init; }
    public required IReadOnlyDictionary<string, AtlasRegionPlacement> Regions { get; init; }
    public required IReadOnlyList<BuiltClip> Clips { get; init; }
    /// <summary>参与打包的源文件数（.aseprite）。</summary>
    public int SourceCount { get; init; }

    public void Dispose() => Image.Dispose();
}

/// <summary>
/// 图集构建器（扩展点）：从源目录「生产」一整张图集（像素 + 区域 + 动画剪辑）。
/// 与 <see cref="ISpriteAtlasSource"/> 互补——后者为已存在的图集 PNG 解析坐标表，
/// 本接口负责上游的生成阶段。一种来源一个实现（<see cref="AsepriteAtlasBuilder"/>，
/// 以后可以是散图文件夹、TexturePacker 工程……）。
/// </summary>
public interface ISpriteAtlasBuilder
{
    BuiltAtlas Build(StorageContainer storage, string directory);
}

/// <summary>
/// Aseprite → 单张项目图集的构建器（Art 管线的核心）。直接解析 .aseprite 二进制
/// （Foster.Framework.Aseprite），拍平可见图层渲染出每一帧（bg/shadow 这类预览图层
/// 默认跳过，见 <see cref="SkipLayers"/>），frame tag 变成命名动画、帧时长（毫秒）
/// 转成秒；装箱用 Foster 的 <see cref="Packer"/>（二叉树装箱 + 透明裁剪 + 边缘出血 +
/// 重复帧合并）。PingPong 标签会展开帧列表（a b c → a b c b），Reverse 反转帧序。
/// </summary>
public sealed class AsepriteAtlasBuilder(int padding = 2, int maxSize = 8192) : ISpriteAtlasBuilder
{
    /// <summary>Flattening skips these layers (case-insensitive): preview backgrounds and ground shadows live in the source files for the Aseprite preview, not for the game.</summary>
    public string[] SkipLayers { get; init; } = ["bg", "shadow"];

    /// <summary>扫描目录（递归）里的所有 .aseprite，渲染帧并装箱成一张图集。</summary>
    public BuiltAtlas Build(StorageContainer storage, string directory)
    {
        var frames = new List<(string Name, Image Image)>();
        var clips = new List<BuiltClip>();
        var sourceCount = 0;

        var files = storage.EnumerateDirectory(directory, "*.aseprite", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var path in files)
        {
            using var stream = storage.OpenRead(path);
            var doc = new Aseprite(stream);
            if (doc.Frames.Length == 0)
                continue;
            sourceCount++;

            var controller = Slug(Path.GetFileNameWithoutExtension(path));
            var rendered = doc.RenderAllFrames(layer => !SkipLayers.Contains(layer.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase));
            var regionNames = new string[rendered.Length];
            for (var index = 0; index < rendered.Length; index++)
            {
                regionNames[index] = $"{controller}/{index}";
                frames.Add((regionNames[index], rendered[index]));
            }

            var durations = new float[doc.Frames.Length];
            for (var index = 0; index < doc.Frames.Length; index++)
                durations[index] = Math.Max(doc.Frames[index].Duration, 1) / 1000f;

            if (doc.Tags.Length == 0)
            {
                clips.Add(new BuiltClip(controller, "default", regionNames, durations, true));
                continue;
            }
            foreach (var tag in doc.Tags)
            {
                var name = Slug(tag.Name ?? "clip");
                var (tagFrames, tagDurations) = ExpandTag(regionNames, durations, tag);
                clips.Add(new BuiltClip(controller, name, tagFrames, tagDurations, true));
            }
        }

        // Foster 的打包器：二叉树装箱 + 透明裁剪 + 边缘出血 + 重复帧合并。裁剪后的
        // 归位信息在 Entry.Frame 里（未裁剪帧内的偏移与原始尺寸），写进区域表。
        var packer = new Packer
        {
            Trim = true,
            Padding = padding,
            DuplicateEdges = padding >= 2,
            CombineDuplicates = true,
            MaxSize = maxSize,
        };
        foreach (var (name, frame) in frames)
        {
            packer.Add(name, frame);
            frame.Dispose(); // Add 即拷贝像素，渲染图到此为止
        }
        var output = packer.Pack();
        if (output.Pages.Count != 1)
            throw new InvalidOperationException($"Atlas content needs {output.Pages.Count} pages; the project atlas is single-texture.");

        var regions = new Dictionary<string, AtlasRegionPlacement>(StringComparer.Ordinal);
        foreach (var entry in output.Entries)
            regions[entry.Name] = new AtlasRegionPlacement(entry.Source, entry.Frame);
        return new BuiltAtlas { Image = output.Pages[0], Regions = regions, Clips = clips, SourceCount = sourceCount };
    }

    /// <summary>展开一个 frame tag 的帧区间（含 Reverse / PingPong）。</summary>
    private static (string[] Frames, float[] Durations) ExpandTag(string[] regions, float[] durations, Aseprite.Tag tag)
    {
        var indices = new List<int>();
        for (var index = tag.From; index <= tag.To; index++)
            indices.Add(index);
        if (tag.LoopDir == Aseprite.LoopDir.Reverse)
            indices.Reverse();
        else if (tag.LoopDir is Aseprite.LoopDir.PingPong or Aseprite.LoopDir.PingPongReverse)
        {
            // a b c → a b c b（PingPongReverse 先反转再补回程）。
            if (tag.LoopDir == Aseprite.LoopDir.PingPongReverse)
                indices.Reverse();
            for (var index = indices.Count - 2; index >= 1; index--)
                indices.Add(indices[index]);
        }
        return (indices.Select(i => regions[i]).ToArray(), indices.Select(i => durations[i]).ToArray());
    }

    /// <summary>把子图名字清洗成稳定的标识符（小写、非字母数字归为下划线）。</summary>
    public static string Slug(string name)
    {
        var builder = new StringBuilder(name.Length);
        var lastUnderscore = true; // 前导不产生下划线
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastUnderscore = false;
            }
            else if (!lastUnderscore)
            {
                builder.Append('_');
                lastUnderscore = true;
            }
        }
        return builder.ToString().TrimEnd('_');
    }
}
