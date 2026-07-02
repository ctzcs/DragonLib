using System.Buffers;
using System.Numerics;
using Foster.Framework;

namespace Engine;

public static class MeshGenerator
{
    public enum PolylineCap
    {
        Butt,
        Square
    }

    /// <summary>
    /// 将一条 2D 折线（polyline）以给定线宽生成描边网格，并提交到 <see cref="Batcher"/>。
    /// </summary>
    /// <param name="batcher">用于提交几何的批处理器。</param>
    /// <param name="points">折线点序列（世界/屏幕坐标均可）。相邻重复点会被忽略。</param>
    /// <param name="width">线宽（单位同 <paramref name="points"/>）。小于等于 0 时按 1 处理。</param>
    /// <param name="color">顶点颜色。</param>
    /// <param name="closed">是否闭合（最后一点与第一点相连）。</param>
    /// <param name="cap">端帽样式（仅对非闭合线有效）。</param>
    /// <remarks>
    /// 这是一个 O(n) 的 stroke 网格生成算法，不做整条轮廓的多边形三角剖分，因此对“涂鸦式自交折线”也不会像耳切那样崩坏。
    /// 核心步骤：
    /// 1) 去除相邻重复点，避免零长度段导致法线与偏移不可定义。
    /// 2) 端帽：非闭合且为 Square 时，将首尾点沿切线方向各外扩 halfWidth，使端面呈方形。
    /// 3) 每个顶点计算一条“偏移向量 offset[i]”：
    ///    - 端点：取相邻段方向 axis，偏移为 axis 的右法线（TurnRight）乘 halfWidth。
    ///    - 中间点：对相邻两段分别求右法线 n0、n1，并求角平分方向 m = n0 + n1。
    ///      mNorm = Normalize(m)，偏移长度为 halfWidth / dot(mNorm, n1)。该式等价于对 miter 的长度校正（dot 项接近 0 表示尖角）。
    ///      为避免尖角产生极长的 miter，本实现对长度做限幅：len ≤ halfWidth * miterLimit（当前常量为 2）。
    ///      若出现反向共线/退化段（m=0 或 denom≈0），回退到单段法线偏移。
    /// 4) 相邻两点 i、j 直接拼接四边形：
    ///    (ci+oi, ci-oi, cj-oj, cj+oj)，形成连续的描边带。
    /// </remarks>
    public static void DrawRibbon(
        Batcher batcher,
        IReadOnlyList<Vector2> points,
        float width,
        in Color color,
        bool closed = false,
        PolylineCap cap = PolylineCap.Square)
    {
        if (points == null || points.Count < 2)
            return;

        if (width <= 0f)
            width = 1f;

        float half = width * 0.5f;

        // de-dup consecutive points (O(n))
        var pts = FramePool<List<Vector2>>.Get();
        pts.Clear();
        pts.EnsureCapacity(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (pts.Count == 0 || pts[^1] != p)
                pts.Add(p);
        }

        if (closed && pts.Count > 1 && pts[0] == pts[^1])
            pts.RemoveAt(pts.Count - 1);

        int n = pts.Count;
        int segCount = closed ? n : n - 1;
        if (segCount <= 0)
            return;

        var poolVec2 = ArrayPool<Vector2>.Shared;
        var centers = poolVec2.Rent(n);
        var offset = poolVec2.Rent(n);

        try
        {
            for (int i = 0; i < n; i++)
                centers[i] = pts[i];

            if (!closed && cap == PolylineCap.Square && n >= 2)
            {
                var a0 = (centers[1] - centers[0]).Normalized();
                var aN = (centers[n - 1] - centers[n - 2]).Normalized();
                centers[0] -= a0 * half;
                centers[n - 1] += aN * half;
            }

            const float miterLimit = 2f;

            for (int i = 0; i < n; i++)
            {
                bool endpoint = !closed && (i == 0 || i == n - 1);

                Vector2 prev = centers[(i - 1 + n) % n];
                Vector2 curr = centers[i];
                Vector2 next = centers[(i + 1) % n];

                if (endpoint)
                {
                    var d = (i == 0) ? (next - curr) : (curr - prev);
                    if (d == Vector2.Zero)
                    {
                        offset[i] = Vector2.Zero;
                        continue;
                    }

                    var axis = Vector2.Normalize(d);
                    offset[i] = axis.TurnRight() * half;
                    continue;
                }

                var d0 = curr - prev;
                var d1 = next - curr;
                if (d0 == Vector2.Zero || d1 == Vector2.Zero)
                {
                    var d = (d1 != Vector2.Zero) ? d1 : d0;
                    if (d == Vector2.Zero)
                    {
                        offset[i] = Vector2.Zero;
                        continue;
                    }

                    var axis = Vector2.Normalize(d);
                    offset[i] = axis.TurnRight() * half;
                    continue;
                }

                var n0 = Vector2.Normalize(d0).TurnRight();
                var n1 = Vector2.Normalize(d1).TurnRight();
                var m = n0 + n1;
                if (m == Vector2.Zero)
                {
                    offset[i] = n1 * half;
                    continue;
                }

                var mNorm = Vector2.Normalize(m);
                float denom = Vector2.Dot(mNorm, n1);
                if (denom <= 1e-4f)
                {
                    offset[i] = n1 * half;
                    continue;
                }

                float len = half / denom;
                float max = half * miterLimit;
                if (len > max)
                    len = max;

                offset[i] = mNorm * len;
            }

            for (int i = 0; i < segCount; i++)
            {
                int j = (i + 1) % n;

                var a = centers[i];
                var b = centers[j];
                var oa = offset[i];
                var ob = offset[j];

                if (oa == Vector2.Zero || ob == Vector2.Zero)
                    continue;

                var a0 = a + oa;
                var a1 = a - oa;
                var b0 = b + ob;
                var b1 = b - ob;

                batcher.Quad(a0, a1, b1, b0, color);
            }
        }
        finally
        {
            poolVec2.Return(centers);
            poolVec2.Return(offset);
        }
    }

    /// <summary>
    /// 将一条 2D 折线（polyline）以给定线宽生成描边网格并贴上 <see cref="Subtexture"/>，然后提交到 <see cref="Batcher"/>。
    /// </summary>
    /// <param name="batcher">用于提交几何的批处理器。</param>
    /// <param name="subtex">用于采样的子纹理区域（提供 Texture 与 TexCoords）。</param>
    /// <param name="points">折线点序列（世界/屏幕坐标均可）。相邻重复点会被忽略。</param>
    /// <param name="width">线宽（单位同 <paramref name="points"/>）。小于等于 0 时按 1 处理。</param>
    /// <param name="color">顶点颜色（会与贴图颜色相乘）。</param>
    /// <param name="closed">是否闭合（最后一点与第一点相连）。</param>
    /// <param name="cap">端帽样式（仅对非闭合线有效）。</param>
    /// <remarks>
    /// 几何生成与无贴图版本一致：去重、端帽外扩、逐点计算（限幅）miter 偏移，然后按段拼接四边形。
    /// 贴图坐标：
    /// - U：沿折线累计长度归一化（dist[i] / totalLen），使纹理在整条线条上连续铺展。
    /// - V：跨线宽方向使用 subtex 的 TexCoords（ty0/ty1）。
    /// - 闭合线在“回到起点”的那一段将末端 U 固定为 1，避免 U 回跳到 0 产生接缝。
    /// </remarks>
    public static void DrawRibbon(
        Batcher batcher,
        in Subtexture subtex,
        IReadOnlyList<Vector2> points,
        float width,
        in Color color,
        bool closed = false,
        PolylineCap cap = PolylineCap.Square,
        float tileLength = 0f)
    {
        if (subtex.IsEmpty || subtex.Texture == null)
            return;
        if (points == null || points.Count < 2)
            return;

        if (width <= 0f)
            width = 1f;

        float half = width * 0.5f;

        var pts = FramePool<List<Vector2>>.Get();
        pts.Clear();
        pts.EnsureCapacity(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (pts.Count == 0 || pts[^1] != p)
                pts.Add(p);
        }

        if (closed && pts.Count > 1 && pts[0] == pts[^1])
            pts.RemoveAt(pts.Count - 1);

        int n = pts.Count;
        int segCount = closed ? n : n - 1;
        if (segCount <= 0)
            return;

        float tx0 = subtex.TexCoords[0].X;
        float tx1 = subtex.TexCoords[1].X;
        float ty0 = subtex.TexCoords[0].Y;
        float ty1 = subtex.TexCoords[2].Y;

        var poolVec2 = ArrayPool<Vector2>.Shared;
        var poolF = ArrayPool<float>.Shared;
        var centers = poolVec2.Rent(n);
        var offset = poolVec2.Rent(n);
        var dist = poolF.Rent(n);

        try
        {
            for (int i = 0; i < n; i++)
                centers[i] = pts[i];

            if (!closed && cap == PolylineCap.Square && n >= 2)
            {
                var a0 = (centers[1] - centers[0]).Normalized();
                var aN = (centers[n - 1] - centers[n - 2]).Normalized();
                centers[0] -= a0 * half;
                centers[n - 1] += aN * half;
            }

            dist[0] = 0f;
            for (int i = 1; i < n; i++)
                dist[i] = dist[i - 1] + (centers[i] - centers[i - 1]).Length();

            float totalLen = closed ? (dist[n - 1] + (centers[0] - centers[n - 1]).Length()) : dist[n - 1];
            if (totalLen <= 0f)
                totalLen = 1f;

            bool tiled = tileLength > 0f;
            float invTileLen = tiled ? (1f / MathF.Max(1e-6f, tileLength)) : 0f;

            const float miterLimit = 2f;

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = centers[(i - 1 + n) % n];
                Vector2 curr = centers[i];
                Vector2 next = centers[(i + 1) % n];

                bool endpoint = !closed && (i == 0 || i == n - 1);

                if (endpoint)
                {
                    var d = (i == 0) ? (next - curr) : (curr - prev);
                    if (d == Vector2.Zero)
                    {
                        offset[i] = Vector2.Zero;
                        continue;
                    }

                    var axis = Vector2.Normalize(d);
                    offset[i] = axis.TurnRight() * half;
                    continue;
                }

                var d0 = curr - prev;
                var d1 = next - curr;
                if (d0 == Vector2.Zero || d1 == Vector2.Zero)
                {
                    var d = (d1 != Vector2.Zero) ? d1 : d0;
                    if (d == Vector2.Zero)
                    {
                        offset[i] = Vector2.Zero;
                        continue;
                    }

                    var axis = Vector2.Normalize(d);
                    offset[i] = axis.TurnRight() * half;
                    continue;
                }

                var n0 = Vector2.Normalize(d0).TurnRight();
                var n1 = Vector2.Normalize(d1).TurnRight();
                var m = n0 + n1;
                if (m == Vector2.Zero)
                {
                    offset[i] = n1 * half;
                    continue;
                }

                var mNorm = Vector2.Normalize(m);
                float denom = Vector2.Dot(mNorm, n1);
                if (denom <= 1e-4f)
                {
                    offset[i] = n1 * half;
                    continue;
                }

                float len = half / denom;
                float max = half * miterLimit;
                if (len > max)
                    len = max;

                offset[i] = mNorm * len;
            }

            var tex = subtex.Texture;
            var col = color;

            static float Frac(float x) => x - MathF.Floor(x);

            void Emit(Vector2 ca, Vector2 cb, Vector2 oa, Vector2 ob, float uA, float uB)
            {
                float fuA = Frac(uA);
                float fuB = Frac(uB);

                if (fuB == 0f && uB > uA)
                    fuB = 1f;

                float uu0 = tx0 + (tx1 - tx0) * fuA;
                float uu1 = tx0 + (tx1 - tx0) * fuB;

                var a0 = ca + oa;
                var a1 = ca - oa;
                var b0 = cb + ob;
                var b1 = cb - ob;

                batcher.Quad(
                    tex,
                    a0,
                    b0,
                    b1,
                    a1,
                    new Vector2(uu0, ty0),
                    new Vector2(uu1, ty0),
                    new Vector2(uu1, ty1),
                    new Vector2(uu0, ty1),
                    col);
            }

            for (int i = 0; i < segCount; i++)
            {
                int j = (i + 1) % n;

                var a = centers[i];
                var b = centers[j];

                var oa = offset[i];
                var ob = offset[j];

                if (oa == Vector2.Zero || ob == Vector2.Zero)
                    continue;

                if (!tiled)
                {
                    float u0 = dist[i] / totalLen;
                    float u1 = closed && j == 0 ? 1f : (dist[j] / totalLen);

                    float uu0 = tx0 + (tx1 - tx0) * u0;
                    float uu1 = tx0 + (tx1 - tx0) * u1;

                    var a0 = a + oa;
                    var a1 = a - oa;
                    var b0 = b + ob;
                    var b1 = b - ob;

                    batcher.Quad(
                        tex,
                        a0,
                        b0,
                        b1,
                        a1,
                        new Vector2(uu0, ty0),
                        new Vector2(uu1, ty0),
                        new Vector2(uu1, ty1),
                        new Vector2(uu0, ty1),
                        col);

                    continue;
                }

                float d0 = dist[i];
                float d1 = (closed && j == 0)
                    ? (dist[n - 1] + (centers[0] - centers[n - 1]).Length())
                    : dist[j];

                float uStart = d0 * invTileLen;
                float uEnd = d1 * invTileLen;

                var curC = a;
                var curO = oa;
                float curU = uStart;

                while (MathF.Floor(curU) < MathF.Floor(uEnd))
                {
                    float nextU = MathF.Floor(curU) + 1f;
                    float t = (uEnd == curU) ? 1f : ((nextU - curU) / (uEnd - curU));
                    t = Mathf.Clamp(t, 0f, 1f);

                    var midC = Vector2.Lerp(curC, b, t);
                    var midO = Vector2.Lerp(curO, ob, t);

                    Emit(curC, midC, curO, midO, curU, nextU);

                    curC = midC;
                    curO = midO;
                    curU = nextU;
                }

                Emit(curC, b, curO, ob, curU, uEnd);
            }
        }
        finally
        {
            poolVec2.Return(centers);
            poolVec2.Return(offset);
            poolF.Return(dist);
        }
    }
}