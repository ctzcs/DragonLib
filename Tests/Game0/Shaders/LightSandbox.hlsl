cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 WorldViewProjection;
    float4x4 World;
};

cbuffer LightSandboxBlock : register(b0, space3)
{
    float4 LightDirection;
    float4 Ambient;
    float4 Diffuse;
};

cbuffer MaterialBlock : register(b1, space3)
{
    float4 Albedo;
    float4 Emissive;
};

#define MAX_POINT_LIGHTS 16

// 与 C# 侧 PointLight3D.Pack 的布局一致（同 Standard3D）。
cbuffer LightSandboxPointLightBlock : register(b2, space3)
{
    float4 PointLightMeta; // x: count
    float4 PointLightPositionRange[MAX_POINT_LIGHTS];  // xyz: position, w: range
    float4 PointLightColorIntensity[MAX_POINT_LIGHTS]; // xyz: color, w: intensity
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
    float3 WorldPosition : TEXCOORD1;
    float4 Color : TEXCOORD2;
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Position = mul(WorldViewProjection, float4(input.Position, 1.0));
    output.WorldPosition = mul(World, float4(input.Position, 1.0)).xyz;
    output.Normal = normalize(mul((float3x3)World, input.Normal));
    output.Color = input.Color;
    return output;
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float3 normal = normalize(input.Normal);
    float3 directional = normalize(-LightDirection.xyz);
    float directAmount = saturate(dot(normal, directional));
    float3 lighting = Ambient.rgb + Diffuse.rgb * directAmount;

    // 点光（无阴影）：smooth window 衰减，range 外为 0。
    int pointLightCount = min((int)PointLightMeta.x, MAX_POINT_LIGHTS);
    for (int i = 0; i < pointLightCount; i++)
    {
        float3 toLight = PointLightPositionRange[i].xyz - input.WorldPosition;
        float distanceToPoint = max(length(toLight), 0.0001);
        float range = max(PointLightPositionRange[i].w, 0.001);
        float pointAmount = saturate(dot(normal, toLight / distanceToPoint));
        float attenuation = saturate(1.0 - distanceToPoint / range);
        attenuation *= attenuation;
        lighting += PointLightColorIntensity[i].rgb * PointLightColorIntensity[i].a
            * pointAmount * attenuation;
    }

    float3 color = input.Color.rgb * Albedo.rgb * lighting + Emissive.rgb * Emissive.a;
    return float4(saturate(color), input.Color.a);
}
