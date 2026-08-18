cbuffer ViewProjectionBlock : register(b0, space1)
{
    float4x4 ViewProjection;
};

cbuffer OrbitBlock : register(b1, space1)
{
    float4 OrbitParams;
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
    float4 World0 : TEXCOORD3;
    float4 World1 : TEXCOORD4;
    float4 World2 : TEXCOORD5;
    float4 World3 : TEXCOORD6;
    float4 InstanceColor : TEXCOORD7;
    float OrbitSpeed : TEXCOORD8;
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
    // Vertex attributes keep Matrix4x4 in C# row order, unlike cbuffer matrices.
    float4x4 world = transpose(float4x4(input.World0, input.World1, input.World2, input.World3));
    float angle = OrbitParams.x * input.OrbitSpeed;
    float sine;
    float cosine;
    sincos(angle, sine, cosine);

    float4 worldPosition = mul(world, float4(input.Position, 1.0));
    worldPosition.xz = float2(
        worldPosition.x * cosine + worldPosition.z * sine,
        -worldPosition.x * sine + worldPosition.z * cosine);

    float3 worldNormal = normalize(mul((float3x3)world, input.Normal));
    worldNormal.xz = float2(
        worldNormal.x * cosine + worldNormal.z * sine,
        -worldNormal.x * sine + worldNormal.z * cosine);

    output.Position = mul(ViewProjection, worldPosition);
    output.Normal = worldNormal;
    output.Color = input.Color * input.InstanceColor;
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
