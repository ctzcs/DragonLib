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
    float4 PointLightPosition;
    float4 PointLightColor;
    float4 PointLightParams;
};

cbuffer MaterialBlock : register(b1, space3)
{
    float4 Albedo;
    float4 Emissive;
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
    float3 pointVector = PointLightPosition.xyz - input.WorldPosition;
    float distanceToPoint = max(length(pointVector), 0.001);
    float3 pointDirection = pointVector / distanceToPoint;
    float pointAmount = saturate(dot(normal, pointDirection));
    float radius = max(PointLightParams.z, 0.01);
    float attenuation = saturate(1.0 - distanceToPoint / radius);
    attenuation *= attenuation;
    float pointEnabled = PointLightParams.x;
    float pointIntensity = PointLightParams.y;
    float3 lighting = Ambient.rgb + Diffuse.rgb * directAmount;
    lighting += PointLightColor.rgb * pointAmount * attenuation * pointIntensity * pointEnabled;
    float3 color = input.Color.rgb * Albedo.rgb * lighting + Emissive.rgb * Emissive.a;
    return float4(saturate(color), input.Color.a);
}
