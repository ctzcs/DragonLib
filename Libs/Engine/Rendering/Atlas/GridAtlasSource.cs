using System.Collections.Generic;
using Foster.Framework;

namespace Engine;

/// <summary>
/// 等分网格图集来源：无坐标文件，按 <c>cellW×cellH</c> 均匀切格，
/// 名字为 <c>"r{行}_c{列}"</c>（从 0 起）。适合简单 tilesheet。
/// </summary>
public sealed class GridAtlasSource : ISpriteAtlasSource
{
    private readonly int _cellW;
    private readonly int _cellH;

    public GridAtlasSource(int cellW, int cellH)
    {
        _cellW = cellW;
        _cellH = cellH;
    }

    public IReadOnlyDictionary<string, RectInt> BuildRects(LocalStorage storage, int atlasWidth, int atlasHeight)
    {
        var rects = new Dictionary<string, RectInt>();
        int cols = atlasWidth / _cellW, rows = atlasHeight / _cellH;
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
            rects[$"r{r}_c{c}"] = new RectInt(c * _cellW, r * _cellH, _cellW, _cellH);
        return rects;
    }

    /// <summary>便捷入口。</summary>
    public static SpriteAtlas Load(GraphicsDevice device, LocalStorage storage, string pngPath, int cellW, int cellH)
        => SpriteAtlas.Load(device, storage, pngPath, new GridAtlasSource(cellW, cellH));
}
