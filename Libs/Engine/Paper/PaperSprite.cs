using System;
using Foster.Framework;
using Prowl.PaperUI;
using Prowl.Quill;
using Prowl.Vector;

namespace Engine.Paper;

/// <summary>
/// 用图集子图给 Paper 元素画背景：整图拉伸 或 九宫格（角不变形、边单向拉、中心平铺拉伸）。
/// 实现走 <c>_paper.Draw((canvas, rect) =&gt; ...)</c>，在回调里用 Quill 的
/// <c>DrawImage(裁好的独立纹理, x, y, w, h)</c> —— 每块都是整张小纹理，绕开图集 UV 矩阵。
/// <para>只依赖 <see cref="SpriteAtlas"/> 的 Get/Crop，对图集来源格式无感知。</para>
/// </summary>
public static class PaperSprite
{
    /// <summary>整张子图拉伸铺满元素（用于图标、箭头这类不需要九宫格的）。</summary>
    public static void DrawSprite(this Prowl.PaperUI.Paper paper, GraphicsDevice device, SpriteAtlas atlas, string sprite, float alpha = 1f)
    {
        var src = atlas.Get(sprite);
        var tex = atlas.Crop(device, src);
        var tint = new Color32((byte)255, (byte)255, (byte)255, (byte)(255 * alpha));
        paper.Draw((canvas, rect) =>
        {
            canvas.DrawImage(tex, (float)rect.Min.X, (float)rect.Min.Y, (float)rect.Size.X, (float)rect.Size.Y, tint);
        });
    }

    /// <summary>
    /// 三段式水平条（Kenney 的 bar：Left 帽 + Mid 拉伸 + Right 帽，各是独立子图）。
    /// fill=1 画满，fill&lt;1 只画到宽度的 fill 比例（用于进度/音量）。竖直居中。
    /// </summary>
    public static void DrawHBar(this Prowl.PaperUI.Paper paper, GraphicsDevice device, SpriteAtlas atlas,
        string left, string mid, string right, float fill = 1f, float alpha = 1f)
    {
        var lTex = atlas.Crop(device, atlas.Get(left));
        var mTex = atlas.Crop(device, atlas.Get(mid));
        var rTex = atlas.Crop(device, atlas.Get(right));
        var lSrc = atlas.Get(left); var rSrc = atlas.Get(right); var mSrc = atlas.Get(mid);
        var tint = new Color32((byte)255, (byte)255, (byte)255, (byte)(255 * alpha));

        paper.Draw((canvas, rect) =>
        {
            float x = (float)rect.Min.X, y = (float)rect.Min.Y;
            float w = (float)rect.Size.X, h = (float)rect.Size.Y;
            float bh = mSrc.Height;                 // 条自身高度（取中段源高）
            float by = y + (h - bh) * 0.5f;         // 竖直居中
            float total = w * Math.Clamp(fill, 0f, 1f);
            float lw = Math.Min(lSrc.Width, total);
            float rw = lSrc.Width + rSrc.Width <= total ? rSrc.Width : Math.Max(0, total - lw);
            float mw = Math.Max(0, total - lw - rw);

            canvas.DrawImage(lTex, x, by, lw, bh, tint);
            if (mw > 0) canvas.DrawImage(mTex, x + lw, by, mw, bh, tint);
            if (rw > 0) canvas.DrawImage(rTex, x + lw + mw, by, rw, bh, tint);
        });
    }

    /// <summary>
    /// 九宫格（四边同一 inset）：inset = 四边不拉伸的边框像素。
    /// 目标尺寸取自元素矩形，边框固定、中间拉伸。
    /// </summary>
    public static void DrawNineSlice(this Prowl.PaperUI.Paper paper, GraphicsDevice device, SpriteAtlas atlas, string sprite, int inset, float alpha = 1f)
        => paper.DrawNineSlice(device, atlas, sprite, inset, inset, inset, inset, alpha);

    /// <summary>九宫格（四边各自 inset）。</summary>
    public static void DrawNineSlice(this Prowl.PaperUI.Paper paper, GraphicsDevice device, SpriteAtlas atlas, string sprite,
        int left, int right, int top, int bottom, float alpha = 1f)
    {
        var src = atlas.Get(sprite);

        // 源图切 9 块（像素矩形）。中间/边的源块尺寸可能为 0（当 inset 覆盖整边），跳过。
        int sx = src.X, sy = src.Y, sw = src.Width, sh = src.Height;
        int mlW = sw - left - right;      // 源中列宽
        int mhH = sh - top - bottom;      // 源中行高
        if (mlW < 1) mlW = 1;
        if (mhH < 1) mhH = 1;

        // 9 块源矩形
        var sTL = new RectInt(sx, sy, left, top);
        var sTM = new RectInt(sx + left, sy, mlW, top);
        var sTR = new RectInt(sx + sw - right, sy, right, top);
        var sML = new RectInt(sx, sy + top, left, mhH);
        var sMM = new RectInt(sx + left, sy + top, mlW, mhH);
        var sMR = new RectInt(sx + sw - right, sy + top, right, mhH);
        var sBL = new RectInt(sx, sy + sh - bottom, left, bottom);
        var sBM = new RectInt(sx + left, sy + sh - bottom, mlW, bottom);
        var sBR = new RectInt(sx + sw - right, sy + sh - bottom, right, bottom);

        // 预先裁好 9 张纹理（带缓存，跨帧只裁一次）
        var tTL = atlas.Crop(device, sTL); var tTM = atlas.Crop(device, sTM); var tTR = atlas.Crop(device, sTR);
        var tML = atlas.Crop(device, sML); var tMM = atlas.Crop(device, sMM); var tMR = atlas.Crop(device, sMR);
        var tBL = atlas.Crop(device, sBL); var tBM = atlas.Crop(device, sBM); var tBR = atlas.Crop(device, sBR);

        var tint = new Color32((byte)255, (byte)255, (byte)255, (byte)(255 * alpha));

        paper.Draw((canvas, rect) =>
        {
            float x = (float)rect.Min.X, y = (float)rect.Min.Y;
            float w = (float)rect.Size.X, h = (float)rect.Size.Y;

            // 目标边框像素（若元素太小，收缩边框避免重叠）
            float l = left, r = right, t = top, b = bottom;
            if (l + r > w) { var k = w / (l + r); l *= k; r *= k; }
            if (t + b > h) { var k = h / (t + b); t *= k; b *= k; }
            float cx = x + l, cy = y + t;               // 中区左上
            float cw = w - l - r, ch = h - t - b;       // 中区宽高
            float rx = x + w - r, by = y + h - b;       // 右列 / 底行起点

            // 四角（固定尺寸）
            canvas.DrawImage(tTL, x, y, l, t, tint);
            canvas.DrawImage(tTR, rx, y, r, t, tint);
            canvas.DrawImage(tBL, x, by, l, b, tint);
            canvas.DrawImage(tBR, rx, by, r, b, tint);
            // 四边（单向拉伸）
            if (cw > 0) { canvas.DrawImage(tTM, cx, y, cw, t, tint); canvas.DrawImage(tBM, cx, by, cw, b, tint); }
            if (ch > 0) { canvas.DrawImage(tML, x, cy, l, ch, tint); canvas.DrawImage(tMR, rx, cy, r, ch, tint); }
            // 中心
            if (cw > 0 && ch > 0) canvas.DrawImage(tMM, cx, cy, cw, ch, tint);
        });
    }
}
