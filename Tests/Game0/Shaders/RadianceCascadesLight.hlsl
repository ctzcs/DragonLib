// Stamps one point light's radial emission into the dynamic emission target.
// The C# side submits each light as a quad whose TexCoord carries the vertex
// WORLD position (same convention as RadianceCascadesStroke.hlsl). Quads are
// drawn with additive blending so overlapping lights sum. Encoding keeps 8x
// HDR headroom in an 8-bit target: rgb = pow(radiance * 1/8, 1/2.2); the RC
// shader decodes with pow(rgb, 2.2) * 8.
//
// The glow profile is a gaussian (sigma = 0.55x radius): a hard solid core
// concentrates a point light's energy into a single probe-ray bin, which is
// what created the long cascade "spokes" — the soft profile spreads emission
// over neighboring bins instead. The matching distance disc keeps the small
// core radius (0.5x), so SDF-guided rays terminate well inside the glow.
// Encoding keeps 8x HDR headroom in an 8-bit target: rgb =
// pow(radiance * 1/8, 1/2.2); the RC shader decodes with pow(rgb, 2.2) * 8.

cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer LightStampBlock : register(b0, space3)
{
    float4 Light;      // xy center, z radius, w intensity
    float4 LightColor; // rgb linear color
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
    // Gaussian falloff instead of a hard solid core: a point emitter's energy
    // concentrated in one probe-ray bin is what creates the long cascade
    // "spokes". A soft profile spreads emission over neighboring bins, so the
    // angular quantization has nothing to alias against. The distance disc
    // keeps the small core radius, so rays still terminate inside the glow.
    float distance = length(input.WorldPosition - Light.xy);
    float sigma = Light.z * 0.7;
    float glow = exp(-distance * distance / max(2.0 * sigma * sigma, 1e-5));
    float3 radiance = LightColor.rgb * Light.w * glow * 0.125;
    return float4(pow(max(radiance, 0.0), 1.0 / 2.2), 1.0);
}
