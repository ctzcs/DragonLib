using System;
using System.Runtime.InteropServices;

namespace Prowl.Scribe.Sdf
{
    /// <summary>Uses msdfgen's edge coloring, overlap support and error correction on Scribe outlines.</summary>
    internal static class MsdfGlyphGenerator
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeVertex
        {
            public int Type, X, Y, Cx, Cy, Cx1, Cy1;
        }

        [DllImport("DragonLib.Msdfgen", EntryPoint = "dragon_msdf_generate", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Generate([In] NativeVertex[] vertices, int count, double scale,
            double x0, double y1, double range, int width, int height, [Out] byte[] rgba);

        public static bool TryGenerate(FontFile font, int glyphIndex, float baseSize, float pxRange, out SdfGlyphResult result)
        {
            result = default;
            var shape = SdfScanlineGenerator.BuildShape(font, glyphIndex);
            if (shape == null) return false; // Whitespace keeps its advance but has no atlas image.

            double scale = font.ScaleForPixelHeight(baseSize);
            if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(baseSize));
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            shape.Bound(ref x0, ref y0, ref x1, ref y1);
            double padding = (pxRange * 0.5 + 1) / scale;
            x0 -= padding; y0 -= padding; x1 += padding; y1 += padding;
            int width = checked((int)Math.Ceiling((x1 - x0) * scale));
            int height = checked((int)Math.Ceiling((y1 - y0) * scale));
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
                throw new InvalidOperationException("MSDF glyph dimensions exceed the supported 4096 pixel limit.");

            int count = font.GetGlyphShape(glyphIndex, out var vertices);
            var native = new NativeVertex[count];
            for (int i = 0; i < count; i++)
            {
                var v = vertices[i];
                native[i] = new NativeVertex { Type = v.type, X = v.x, Y = v.y, Cx = v.cx, Cy = v.cy, Cx1 = v.cx1, Cy1 = v.cy1 };
            }
            byte[] rgba = new byte[checked(width * height * 4)];
            int status;
            try { status = Generate(native, count, scale, x0, y1, pxRange, width, height, rgba); }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException("MSDF requires DragonLib.Msdfgen for the current platform. Build Libs/ThirdParty/Msdfgen and deploy its native library beside Scribe.dll.", ex);
            }
            if (status != 0)
                throw new InvalidOperationException($"MSDF generation failed for glyph {glyphIndex} (native status {status}).");

            // Use the exact texel extent after rounding; otherwise quads stretch the field slightly.
            result = new SdfGlyphResult { Rgba = rgba, Width = width, Height = height,
                Rx0 = x0, Ry0 = y1 - height / scale, Rx1 = x0 + width / scale, Ry1 = y1 };
            return true;
        }
    }
}
