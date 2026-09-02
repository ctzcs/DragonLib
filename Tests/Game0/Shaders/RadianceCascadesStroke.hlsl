// Bakes one primitive's SDF into a distance-field target. The C# side submits
// each primitive as a quad whose TexCoord carries the vertex WORLD position
// (the batcher's Matrix slot holds the paint-target ortho projection, so
// SV-space coordinates are pixels, not world units). Quads are drawn with min
// blending, so the R channel keeps the smallest (nearest-surface) distance
// everywhere. Encoding:
// R = saturate((distance - lo) / (hi - lo)), matching the Paint uniform
// decode in RadianceCascades.hlsl.
//
// Primitive type (Params.z): 0 = capsule segment (Segment xy=start, zw=end,
// radius Params.x), 1 = circle (Segment xy=center, radius Params.x),
// 2 = round box (Segment xy=center, zw=half size, corner radius Params.x).

cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer StrokeFieldBlock : register(b0, space3)
{
    float4 Segment; // see primitive type above
    float4 Params;  // x radius, y encode scale (hi - lo), z primitive type, w encode offset (lo)
};

struct VsInput
{
    float2 Position : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float4 Color : TEXCOORD2;
    float4 Mode : TEXCOORD4;
};

struct VsOutput
{
    float2 WorldPosition : TEXCOORD0;
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.WorldPosition = input.TexCoord;
    output.Position = mul(Matrix, float4(input.Position, 0.0, 1.0));
    return output;
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float2 p = input.WorldPosition;
    float distance;
    if (Params.z > 1.5)
    {
        float2 q = abs(p - Segment.xy) - Segment.zw + Params.x;
        distance = min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - Params.x;
    }
    else if (Params.z > 0.5)
    {
        distance = length(p - Segment.xy) - Params.x;
    }
    else
    {
        float2 ab = Segment.zw - Segment.xy;
        float h = saturate(dot(p - Segment.xy, ab) / max(dot(ab, ab), 0.0001));
        distance = length(p - Segment.xy - ab * h) - Params.x;
    }
    float encoded = saturate((distance - Params.w) / Params.y);
    return float4(encoded, encoded, encoded, 1.0);
}
