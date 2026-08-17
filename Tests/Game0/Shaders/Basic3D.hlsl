cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 WorldViewProjection;
    float4x4 World;
};

cbuffer Basic3DLightBlock : register(b0, space3)
{
    float4 LightDirection;
    float4 Ambient;
    float4 Diffuse;
};

struct VsInput
{
    float3 Position : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float4 Color : TEXCOORD2;
};

struct VsOutput
{
    float3 Normal : TEXCOORD0;
    float4 Color : TEXCOORD1;
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Position = mul(WorldViewProjection, float4(input.Position, 1.0));
    output.Normal = normalize(mul((float3x3)World, input.Normal));
    output.Color = input.Color;
    return output;
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float3 normal = normalize(input.Normal);
    float3 light = normalize(-LightDirection.xyz);
    float intensity = saturate(dot(normal, light));
    float3 color = input.Color.rgb * (Ambient.rgb + Diffuse.rgb * intensity);
    return float4(saturate(color), input.Color.a);
}
