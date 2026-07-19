using System;
using System.Collections.Generic;
using System.IO;
using Foster.Framework;

namespace Engine;

/// <summary>
/// 图集来源（扩展点）：把某种格式解析成「名字 → 像素矩形」。
/// 一种格式一个实现（<see cref="KenneyXmlAtlasSource"/> / <see cref="GridAtlasSource"/> …），
/// 核心 <see cref="SpriteAtlas"/> 不认识任何具体格式，只消费本接口的产出。
/// </summary>
public interface ISpriteAtlasSource
{
    /// <summary>
    /// 产出子图矩形表。<paramref name="storage"/> 用于读可能的坐标文件；
    /// <paramref name="atlasWidth"/>/<paramref name="atlasHeight"/> 是纹理像素尺寸（等分网格等按尺寸算的实现会用到）。
    /// </summary>
    IReadOnlyDictionary<string, RectInt> BuildRects(LocalStorage storage, int atlasWidth, int atlasHeight);
}

/// <summary>
/// 通用图集：一张纹理 + 「名字 → 像素矩形」映射。<b>与来源格式无关</b>——
/// 核心只提供查询（<see cref="Get"/>）和裁剪（<see cref="Crop"/>）。
/// 具体格式由 <see cref="ISpriteAtlasSource"/> 的实现负责，经 <see cref="Load"/> 注入。
/// </summary>
public sealed class SpriteAtlas : IDisposable
{
    public Texture Texture { get; }
    public int Width => Texture.Width;
    public int Height => Texture.Height;

    private readonly Dictionary<string, RectInt> _sprites;
    private readonly Dictionary<string, Texture> _cropCache = new();
    private readonly List<Texture> _ownedCrops = new();
    private Image? _sourceImage;   // 源像素，裁剪时用；加载后保留

    private SpriteAtlas(Texture texture, Dictionary<string, RectInt> sprites, Image sourceImage)
    {
        Texture = texture;
        _sprites = sprites;
        _sourceImage = sourceImage;
    }

    // ============ 核心查询 / 裁剪（与来源无关） ============

    /// <summary>按名字取子图像素矩形，找不到抛异常。</summary>
    public RectInt Get(string name)
    {
        if (_sprites.TryGetValue(name, out var r))
            return r;
        throw new KeyNotFoundException($"SpriteAtlas 里没有子图 '{name}'。");
    }

    public bool Has(string name) => _sprites.ContainsKey(name);

    /// <summary>所有子图名字（调试 / 枚举用）。</summary>
    public IReadOnlyCollection<string> Names => _sprites.Keys;

    /// <summary>
    /// 把某个像素矩形裁成一张独立小纹理（带缓存）。用于九宫格的角/边/中块——
    /// 走整图绘制路径，绕开图集 UV 矩阵的方向坑。
    /// </summary>
    public Texture Crop(GraphicsDevice device, RectInt src)
    {
        var key = $"{src.X},{src.Y},{src.Width},{src.Height}";
        if (_cropCache.TryGetValue(key, out var cached))
            return cached;

        if (_sourceImage == null)
            throw new InvalidOperationException("源图已释放，无法再裁剪。");

        int w = src.Width, h = src.Height;
        var pixels = new Color[w * h];
        var srcData = _sourceImage.Data;
        int tw = _sourceImage.Width;
        for (var row = 0; row < h; row++)
        {
            var srcStart = (src.Y + row) * tw + src.X;
            srcData.Slice(srcStart, w).CopyTo(pixels.AsSpan(row * w, w));
        }

        var tex = new Texture(device, w, h, pixels, name: $"crop_{key}");
        _cropCache[key] = tex;
        _ownedCrops.Add(tex);
        return tex;
    }

    public void Dispose()
    {
        foreach (var t in _ownedCrops)
            if (!t.IsDisposed) t.Dispose();
        _ownedCrops.Clear();
        _cropCache.Clear();
        if (!Texture.IsDisposed) Texture.Dispose();
        _sourceImage?.Dispose();
        _sourceImage = null;
    }

    // ============ 构造 ============

    /// <summary>
    /// 通用加载：读 PNG，交给 <paramref name="source"/> 解析出矩形表，组装成图集。
    /// 换格式只需换 <see cref="ISpriteAtlasSource"/> 实现，本方法与调用端都不用改。
    /// </summary>
    public static SpriteAtlas Load(GraphicsDevice device, LocalStorage storage, string pngPath, ISpriteAtlasSource source)
    {
        var image = ReadImage(storage, pngPath);
        var rects = source.BuildRects(storage, image.Width, image.Height);
        return FromRects(device, image, rects, name: pngPath);
    }

    /// <summary>
    /// 从已加载的纹理 + 矩形字典直接构造（不经来源解析）。
    /// 图集接管 <paramref name="image"/> 的生命周期（Dispose 时释放）。
    /// </summary>
    public static SpriteAtlas FromRects(GraphicsDevice device, Image image, IReadOnlyDictionary<string, RectInt> rects, string? name = null)
    {
        var texture = new Texture(device, image, name: name ?? "SpriteAtlas");
        return new SpriteAtlas(texture, new Dictionary<string, RectInt>(rects), image);
    }

    /// <summary>从 storage 读 PNG 成 Image（保留像素供裁剪）。source 实现如需读坐标文件，请自行用 storage.OpenRead。</summary>
    public static Image ReadImage(LocalStorage storage, string pngPath)
    {
        using var stream = storage.OpenRead(pngPath);
        return new Image(stream);
    }
}
