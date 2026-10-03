#include "Standard3DCommon.hlsli"
struct VsInput
{
    float3 Position : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 Uv : TEXCOORD2;
    float4 Tangent : TEXCOORD3;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Position = mul(WorldViewProjection, float4(input.Position, 1.0));
    output.Normal = normalize(mul((float3x3)World, input.Normal));
    output.Tangent = float4(normalize(mul((float3x3)World, input.Tangent.xyz)), input.Tangent.w);
    output.Uv = input.Uv;
    float3 worldPosition = mul(World, float4(input.Position, 1.0)).xyz;
    output.WorldPosition = worldPosition;
    output.ShadowPosition = mul(LightViewProjection, float4(worldPosition, 1.0));
    return output;
}

