#include "PostFullscreen.hlsli"
cbuffer CompositeSettings : register(b0, space3) { float4 Settings; }; // exposure, tonemap mode, HDR, bloom intensity
Texture2D SceneTexture : register(t0, space2);
SamplerState SceneSampler : register(s0, space2);
Texture2D BloomTexture : register(t1, space2);
SamplerState BloomSampler : register(s1, space2);
float4 fragment_main(VsOutput input) : SV_Target0
{
    float4 source = SceneTexture.Sample(SceneSampler, input.Uv);
    if (Settings.z < .5) return source;
    float3 x = max((source.rgb + BloomTexture.Sample(BloomSampler, input.Uv).rgb * Settings.w) * Settings.x, 0);
    float3 color = Settings.y > .5 ? x / (1 + x) : saturate(x * (2.51 * x + .03) / (x * (2.43 * x + .59) + .14));
    return float4(EncodeSrgb(color), source.a);
}
