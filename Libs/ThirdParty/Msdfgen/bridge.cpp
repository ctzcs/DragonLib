#include "msdfgen.h"
#include <algorithm>
#include <cmath>
#include <cstdint>

#ifdef _WIN32
#define EXPORT extern "C" __declspec(dllexport)
#else
#define EXPORT extern "C" __attribute__((visibility("default")))
#endif

// Stable C ABI; no native allocation or exception crosses the boundary.
struct GlyphVertex { int32_t type, x, y, cx, cy, cx1, cy1; };
EXPORT int dragon_msdf_generate(const GlyphVertex *vertices, int count, double scale,
    double x0, double y1, double range, int width, int height, unsigned char *rgba) {
    if (!vertices || !rgba || count <= 0 || width <= 0 || height <= 0 ||
        width > 4096 || height > 4096 || !std::isfinite(scale) || scale <= 0 ||
        !std::isfinite(range) || range <= 0 || !std::isfinite(x0) || !std::isfinite(y1)) return 1;
    try {
        using namespace msdfgen;
        Shape shape;
        Point2 current;
        for (int i = 0; i < count; ++i) {
            const GlyphVertex &v = vertices[i];
            Point2 to(v.x, v.y);
            if (v.type == 1) shape.addContour();
            else {
                if (shape.contours.empty()) return 2;
                Contour &c = shape.contours.back();
                switch (v.type) {
                    case 2: if (current != to) c.addEdge(EdgeHolder(current, to)); break;
                    case 3: c.addEdge(EdgeHolder(current, Point2(v.cx, v.cy), to)); break;
                    case 4: c.addEdge(EdgeHolder(current, Point2(v.cx, v.cy), Point2(v.cx1, v.cy1), to)); break;
                    default: return 2;
                }
            }
            current = to;
        }
        if (!shape.validate()) return 2;
        shape.normalize();
        edgeColoringSimple(shape, 3.0);
        // msdfgen works Y-up. Flip the completed bitmap for Quill's top-down atlas.
        Projection projection(Vector2(scale), Vector2(-x0, height / scale - y1));
        SDFTransformation transform(projection, Range(range / scale));
        Bitmap<float, 3> bitmap(width, height);
        MSDFGeneratorConfig config;
        generateMSDF(bitmap, shape, transform, config);
        distanceSignCorrection(bitmap, shape, projection, FILL_NONZERO);
        msdfErrorCorrection(bitmap, shape, transform, config);
        for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x) {
                const float *pixel = bitmap(x, height - 1 - y);
                unsigned char *out = rgba + 4 * (y * width + x);
                for (int c = 0; c < 3; ++c)
                    out[c] = static_cast<unsigned char>(std::max(0., std::min(255., std::floor(256. * pixel[c]))));
                out[3] = 255;
            }
        return 0;
    } catch (...) { return 3; }
}
