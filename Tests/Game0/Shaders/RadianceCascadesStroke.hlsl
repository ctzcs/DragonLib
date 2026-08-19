// Bakes one emissive stroke's capsule SDF into the stroke distance-field
// target. The C# side submits each segment as a quad whose TexCoord carries
// the vertex WORLD position (the batcher's Matrix slot holds the paint-target
// ortho projection, so SV-space coordinates are pixels, not world units).
// Quads are drawn with min blending, so the R channel keeps the smallest
// (nearest-stroke) distance everywhere. Encoding:
// R = saturate((distance - lo) / (hi - lo)), matching the Paint uniform
// decode in RadianceCascades.hlsl.

cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer StrokeFieldBlock : register(b0, space3)
{
    float4 Segment; // xy start, zw end, world units
    float4 Params;  // x radius, y encode scale (hi - lo), z unused, w encode offset (lo)
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
    float2 ab = Segment.zw - Segment.xy;
    float h = saturate(dot(input.WorldPosition - Segment.xy, ab) / max(dot(ab, ab), 0.0001));
    float distance = length(input.WorldPosition - Segment.xy - ab * h) - Params.x;
    float encoded = saturate((distance - Params.w) / Params.y);
    return float4(encoded, encoded, encoded, 1.0);
}
