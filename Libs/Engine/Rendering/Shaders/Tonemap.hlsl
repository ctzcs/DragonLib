cbuffer MatrixBlock : register(b0, space1) { float4x4 Matrix; };
cbuffer TonemapBlock : register(b0, space3) { float4 Settings; };
Texture2D HdrTexture : register(t0, space2);
SamplerState HdrSampler : register(s0, space2);
struct VsInput
{
    float2 Position : TEXCOORD0;
    float2 Uv : TEXCOORD1;
};
struct VsOutput { float2 Uv : TEXCOORD0; float4 Position : SV_Position; };
VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Uv = input.Uv;
    output.Position = mul(Matrix, float4(input.Position, 0.0, 1.0));
    return output;
}
float3 EncodeSrgb(float3 value)
{
    return float3(value.x <= 0.0031308 ? value.x * 12.92 : 1.055 * pow(value.x, 1.0 / 2.4) - 0.055,
                  value.y <= 0.0031308 ? value.y * 12.92 : 1.055 * pow(value.y, 1.0 / 2.4) - 0.055,
                  value.z <= 0.0031308 ? value.z * 12.92 : 1.055 * pow(value.z, 1.0 / 2.4) - 0.055);
}
float4 fragment_main(VsOutput input) : SV_Target0
{
    float4 sampled = HdrTexture.Sample(HdrSampler, input.Uv);
    float3 x = max(sampled.rgb * Settings.x, 0.0);
    float3 color = Settings.y > 0.5 ? x / (1.0 + x) : saturate(x * (2.51 * x + 0.03) / (x * (2.43 * x + 0.59) + 0.14));
    if (Settings.z > 0.5) color = EncodeSrgb(color);
    return float4(color, sampled.a);
}
