#include "PostFullscreen.hlsli"
cbuffer FxaaSettings : register(b0, space3) { float4 Texel; };
Texture2D SourceTexture : register(t0, space2);
SamplerState SourceSampler : register(s0, space2);
float Luma(float3 color) { return dot(color, float3(.299, .587, .114)); }
float4 fragment_main(VsOutput input) : SV_Target0
{
    float4 center = SourceTexture.Sample(SourceSampler, input.Uv);
    float nw = Luma(SourceTexture.Sample(SourceSampler, input.Uv + Texel.xy * float2(-1, -1)).rgb);
    float ne = Luma(SourceTexture.Sample(SourceSampler, input.Uv + Texel.xy * float2(1, -1)).rgb);
    float sw = Luma(SourceTexture.Sample(SourceSampler, input.Uv + Texel.xy * float2(-1, 1)).rgb);
    float se = Luma(SourceTexture.Sample(SourceSampler, input.Uv + Texel.xy * float2(1, 1)).rgb);
    float c = Luma(center.rgb);
    float minimum = min(c, min(min(nw, ne), min(sw, se)));
    float maximum = max(c, max(max(nw, ne), max(sw, se)));
    if (maximum - minimum < max(.0312, maximum * .125)) return center;
    float2 direction = float2(-(nw + ne - sw - se), nw + sw - ne - se);
    float reduction = max((nw + ne + sw + se) * .03125, 1 / 128.0);
    direction = clamp(direction / (min(abs(direction.x), abs(direction.y)) + reduction), -8, 8) * Texel.xy;
    float3 a = .5 * (SourceTexture.Sample(SourceSampler, input.Uv - direction / 6).rgb +
        SourceTexture.Sample(SourceSampler, input.Uv + direction / 6).rgb);
    float3 b = a * .5 + .25 * (SourceTexture.Sample(SourceSampler, input.Uv - direction * .5).rgb +
        SourceTexture.Sample(SourceSampler, input.Uv + direction * .5).rgb);
    float luma = Luma(b);
    return float4(luma < minimum || luma > maximum ? a : b, center.a);
}
