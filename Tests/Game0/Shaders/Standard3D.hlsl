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

cbuffer ShadowMatrixBlock : register(b1, space1)
{
    float4x4 LightViewProjection;
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

cbuffer Standard3DShadowBlock : register(b2, space3)
{
    float4 ShadowSettings; // x: enabled, y: texel size, z: bias, w: darkness
};

Texture2D AlbedoTexture : register(t0, space2);
SamplerState AlbedoSampler : register(s0, space2);

Texture2D NormalTexture : register(t1, space2);
SamplerState NormalSampler : register(s1, space2);

Texture2D ShadowMapTexture : register(t2, space2);
SamplerState ShadowSampler : register(s2, space2);

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
    float4 ShadowPosition : TEXCOORD3;
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Position = mul(WorldViewProjection, float4(input.Position, 1.0));
    output.Normal = normalize(mul((float3x3)World, input.Normal));
    output.Tangent = float4(normalize(mul((float3x3)World, input.Tangent.xyz)), input.Tangent.w);
    output.Uv = input.Uv;
    float3 worldPosition = mul(World, float4(input.Position, 1.0)).xyz;
    output.ShadowPosition = mul(LightViewProjection, float4(worldPosition, 1.0));
    return output;
}

float ComputeShadow(VsOutput input)
{
    if (ShadowSettings.x < 0.5)
        return 1.0;

    // 行向量矩阵乘出的裁剪空间：xyz 除以 w 后 x/y ∈ [-1,1]（映射到 uv），z 即深度。
    float3 ndc = input.ShadowPosition.xyz / input.ShadowPosition.w;
    float2 uv = ndc.xy * 0.5 + 0.5;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return 1.0; // 光照正交框外：不投影。

    float texel = ShadowSettings.y;
    float bias = ShadowSettings.z;
    float darkness = ShadowSettings.w;

    float lit = 0.0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 offset = float2(x, y) * texel;
            float depth = ShadowMapTexture.Sample(ShadowSampler, uv + offset).r;
            lit += (ndc.z - bias) <= depth ? 1.0 : 0.0;
        }
    }

    float shadow = lit / 9.0;
    return 1.0 - darkness * (1.0 - shadow);
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

    float shadow = ComputeShadow(input);
    float3 light = normalize(-LightDirection.xyz);
    float intensity = saturate(dot(normal, light)) * shadow;
    float3 color = baseColor.rgb * (Ambient.rgb + Diffuse.rgb * intensity);
    return float4(saturate(color), baseColor.a);
}
