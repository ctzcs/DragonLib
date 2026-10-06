#include "PostFullscreen.hlsli"
cbuffer DepthSettings : register(b0, space3)
{
    float4x4 InverseProjection;
    float4 FogColor;
    float4 Fog; // mode(0 off,1 linear,2 exp,3 exp2), start, end, density
    float4 Ao; // enabled, radius(world units), strength, bias
    float4 Viewport; // width,height,projection y scale,HDR
};
Texture2D SceneTexture : register(t0, space2);
SamplerState SceneSampler : register(s0, space2);
Texture2D DepthTexture : register(t1, space2);
SamplerState DepthSampler : register(s1, space2);

float3 ViewPosition(float2 uv, float depth)
{
    float4 p = mul(InverseProjection, float4(uv * float2(2, -2) + float2(-1, 1), depth, 1));
    return p.xyz / p.w;
}
float4 fragment_main(VsOutput input) : SV_Target0
{
    float4 source = SceneTexture.Sample(SceneSampler, input.Uv);
    float depth = DepthTexture.Sample(DepthSampler, input.Uv).r;
    float3 p = ViewPosition(input.Uv, depth);
    // 导数必须在分支外求值，避免轮廓像素邻居跨分支导致未定义的法线。
    float3 n = cross(ddx(p), ddy(p));
    n /= max(length(n), 1e-6);
    if (dot(n, -p) < 0) n = -n;
    if (depth >= .999999) return source;
    float3 color = Viewport.w > .5 ? source.rgb : DecodeSrgb(source.rgb);
    if (Ao.x > .5)
    {
        float pixelRadius = clamp(Ao.y * Viewport.z / max(-p.z, .001) * .5, 1 / Viewport.y, .15);
        float occlusion = 0;
        // 深度重建的视空间邻域：同平面的点不会遮蔽自己，离屏样本也不夹到边界制造暗线。
        for (int i = 0; i < 16; i++)
        {
            float angle = i * 2.399963;
            float radius = sqrt((i + .5) / 16.0);
            float2 uv = input.Uv + float2(cos(angle) * Viewport.y / Viewport.x, sin(angle)) * pixelRadius * radius;
            if (any(uv < 0) || any(uv > 1)) continue;
            float sampleDepth = DepthTexture.Sample(DepthSampler, uv).r;
            if (sampleDepth >= .999999) continue;
            float3 delta = ViewPosition(uv, sampleDepth) - p;
            float distance = length(delta);
            occlusion += saturate((dot(n, delta) - Ao.w) / max(distance, .001)) *
                saturate(1 - distance / max(Ao.y, .001));
        }
        color *= saturate(1 - occlusion * Ao.z / 16);
    }
    float distance = length(p);
    float fog = Fog.x < .5 ? 0 : Fog.x < 1.5 ? saturate((distance - Fog.y) / max(Fog.z - Fog.y, .001)) :
        Fog.x < 2.5 ? 1 - exp(-Fog.w * distance) : 1 - exp(-pow(Fog.w * distance, 2));
    color = lerp(color, FogColor.rgb, fog);
    return float4(Viewport.w > .5 ? color : EncodeSrgb(color), source.a);
}
