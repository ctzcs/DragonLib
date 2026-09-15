// Standard3D: PositionNormalUvVertex (Position TEXCOORD0 / Normal TEXCOORD1 /
// Uv TEXCOORD2 / Tangent TEXCOORD3) + albedo/normal map sampling.
// Vertex uniforms match Renderer3D's DirectVertexUniforms (WVP + World).
// Each texture gets its OWN SamplerState: shadercross classifies textures
// that share one SamplerState as storage textures, which Foster never binds.

cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 WorldViewProjection;
    float4x4 World;
};

cbuffer Standard3DLightBlock : register(b0, space3)
{
    float4 LightDirection;
    float4 Ambient;
    float4 Diffuse;
};

cbuffer Standard3DMaterialBlock : register(b1, space3)
{
    float4 BaseColorFactor;
    float4 MaterialFlags; // x: has albedo, y: has normal map, z: normal strength, w: unused
};

Texture2D AlbedoTexture : register(t0, space2);
SamplerState AlbedoSampler : register(s0, space2);

Texture2D NormalTexture : register(t1, space2);
SamplerState NormalSampler : register(s1, space2);

struct VsInput
{
    float3 Position : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 Uv : TEXCOORD2;
    float4 Tangent : TEXCOORD3;
};

struct VsOutput
{
    float3 Normal : TEXCOORD0;
    float4 Tangent : TEXCOORD1;
    float2 Uv : TEXCOORD2;
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Position = mul(WorldViewProjection, float4(input.Position, 1.0));
    output.Normal = normalize(mul((float3x3)World, input.Normal));
    output.Tangent = float4(normalize(mul((float3x3)World, input.Tangent.xyz)), input.Tangent.w);
    output.Uv = input.Uv;
    return output;
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float4 baseColor = BaseColorFactor;
    if (MaterialFlags.x > 0.5)
        baseColor *= AlbedoTexture.Sample(AlbedoSampler, input.Uv);

    float3 normal = normalize(input.Normal);
    if (MaterialFlags.y > 0.5)
    {
        // TBN basis; tangent.w carries bitangent handedness.
        float3 tangent = normalize(input.Tangent.xyz);
        float3 bitangent = cross(normal, tangent) * input.Tangent.w;
        float3x3 tbn = float3x3(tangent, bitangent, normal);

        float3 sampled = NormalTexture.Sample(NormalSampler, input.Uv).xyz * 2.0 - 1.0;
        sampled.xy *= MaterialFlags.z;
        normal = normalize(mul(sampled, tbn));
    }

    float3 light = normalize(-LightDirection.xyz);
    float intensity = saturate(dot(normal, light));
    float3 color = baseColor.rgb * (Ambient.rgb + Diffuse.rgb * intensity);
    return float4(saturate(color), baseColor.a);
}
