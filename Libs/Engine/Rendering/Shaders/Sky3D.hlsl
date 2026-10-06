#include "PostFullscreen.hlsli"
#include "EnvironmentUv.hlsli"
cbuffer SkySettings : register(b0, space3)
{
    float4x4 InverseViewProjection;
    float4 CameraPosition;
    float4 Settings; // intensity, rotation radians, HDR, unused
};
Texture2D SkyTexture : register(t0, space2);
SamplerState SkySampler : register(s0, space2);
float4 fragment_main(VsOutput input) : SV_Target0
{
    float4 farPoint = mul(InverseViewProjection, float4(input.Uv * float2(2, -2) + float2(-1, 1), 1, 1));
    float3 direction = normalize(farPoint.xyz / farPoint.w - CameraPosition.xyz);
    float3 color = SkyTexture.Sample(SkySampler, EnvironmentUv(RotateEnvironment(direction, Settings.y))).rgb * Settings.x;
    return float4(Settings.z > .5 ? color : EncodeSrgb(color), 1);
}
