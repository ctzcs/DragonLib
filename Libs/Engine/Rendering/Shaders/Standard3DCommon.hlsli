// 静态与蒙皮共用片元管线；每张贴图必须独占 sampler，避免 shadercross 错判 storage texture。
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
    float4 CameraPosition; // xyz: 相机世界位置（镜面项要 view 方向）
    float4 ColorPipeline; // x: HDR 开关，LDR 保留原有 gamma 光照。
};

cbuffer Standard3DMaterialBlock : register(b1, space3)
{
    float4 BaseColorFactor;
    float4 MaterialFlags; // x: has albedo, y: has normal map, z: normal strength, w: alpha mode (0 opaque, 1 mask, 2 blend)
    float4 AlphaParams;   // x: alpha cutoff (mask mode)
    float4 PbrParams;     // x: metallic, y: roughness, z: albedo 已由 sRGB 纹理硬件解码
    float4 TextureFlags;  // MR、AO、emissive、emissive sRGB 硬件解码
    float4 Emissive;      // xyz: factor；w: AO strength
};

cbuffer Standard3DShadowBlock : register(b2, space3)
{
    float4 ShadowSettings; // x: enabled, y: texel size, z: bias, w: darkness
};

#define MAX_POINT_LIGHTS 16

// 与 C# 侧 PointLight3D.Pack 的布局一致。
cbuffer Standard3DPointLightBlock : register(b3, space3)
{
    float4 PointLightMeta; // x: count
    float4 PointLightPositionRange[MAX_POINT_LIGHTS];  // xyz: position, w: range
    float4 PointLightColorIntensity[MAX_POINT_LIGHTS]; // xyz: color, w: intensity
};

Texture2D AlbedoTexture : register(t0, space2);
SamplerState AlbedoSampler : register(s0, space2);

Texture2D NormalTexture : register(t1, space2);
SamplerState NormalSampler : register(s1, space2);

Texture2D ShadowMapTexture : register(t2, space2);
SamplerState ShadowSampler : register(s2, space2);

Texture2D MetallicRoughnessTexture : register(t3, space2);
SamplerState MetallicRoughnessSampler : register(s3, space2);
Texture2D OcclusionTexture : register(t4, space2);
SamplerState OcclusionSampler : register(s4, space2);
Texture2D EmissiveTexture : register(t5, space2);
SamplerState EmissiveSampler : register(s5, space2);

struct VsOutput
{
    float3 Normal : TEXCOORD0;
    float4 Tangent : TEXCOORD1;
    float2 Uv : TEXCOORD2;
    float4 ShadowPosition : TEXCOORD3;
    float3 WorldPosition : TEXCOORD4;
    float4 Position : SV_Position;
};

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

// Cook-Torrance GGX 三件：NDF / Geometry / Fresnel。
float DistributionGGX(float3 normal, float3 halfVector, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float nDotH = saturate(dot(normal, halfVector));
    float denom = nDotH * nDotH * (a2 - 1.0) + 1.0;
    return a2 / (3.14159265 * denom * denom);
}

float GeometrySchlickGGX(float nDotV, float roughness)
{
    float k = roughness + 1.0;
    k = k * k / 8.0;
    return nDotV / (nDotV * (1.0 - k) + k);
}

float3 FresnelSchlick(float cosTheta, float3 f0)
{
    return f0 + (1.0 - f0) * pow(1.0 - cosTheta, 5.0);
}

// 单个光源的 Cook-Torrance 贡献。radiance 已含衰减/阴影；漫反射不除 π，
// 光照强度直接当亮度参数用（metallic=0/roughness=1 时 ≈ 原 Lambert 观感）。
float3 ShadeCookTorrance(
    float3 normal, float3 viewDir, float3 lightDir, float3 radiance,
    float3 albedo, float metallic, float roughness)
{
    float3 halfVector = normalize(viewDir + lightDir);
    float nDotL = saturate(dot(normal, lightDir));
    float nDotV = saturate(dot(normal, viewDir));

    float3 f0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    float3 f = FresnelSchlick(saturate(dot(halfVector, viewDir)), f0);
    float d = DistributionGGX(normal, halfVector, roughness);
    float g = GeometrySchlickGGX(nDotV, roughness) * GeometrySchlickGGX(nDotL, roughness);
    float3 specular = d * g * f / max(4.0 * nDotV * nDotL, 1e-4);

    float3 diffuse = (1.0 - f) * (1.0 - metallic) * albedo;
    return (diffuse + specular) * radiance * nDotL;
}

float3 SrgbToLinear(float3 v)
{
    return float3(v.x <= 0.04045 ? v.x / 12.92 : pow((v.x + 0.055) / 1.055, 2.4),
                  v.y <= 0.04045 ? v.y / 12.92 : pow((v.y + 0.055) / 1.055, 2.4),
                  v.z <= 0.04045 ? v.z / 12.92 : pow((v.z + 0.055) / 1.055, 2.4));
}
float3 LinearToSrgb(float3 v)
{
    return float3(v.x <= 0.0031308 ? v.x * 12.92 : 1.055 * pow(v.x, 1.0 / 2.4) - 0.055,
                  v.y <= 0.0031308 ? v.y * 12.92 : 1.055 * pow(v.y, 1.0 / 2.4) - 0.055,
                  v.z <= 0.0031308 ? v.z * 12.92 : 1.055 * pow(v.z, 1.0 / 2.4) - 0.055);
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float4 baseColor = BaseColorFactor;
    if (MaterialFlags.x > 0.5)
    {
        float4 albedoSample = AlbedoTexture.Sample(AlbedoSampler, input.Uv);
        if (ColorPipeline.x > 0.5 && PbrParams.z < 0.5) albedoSample.rgb = SrgbToLinear(albedoSample.rgb);
        if (ColorPipeline.x < 0.5 && PbrParams.z > 0.5) albedoSample.rgb = LinearToSrgb(albedoSample.rgb);
        baseColor *= albedoSample;
    }

    // alpha cutout：mask 模式下低于 cutoff 的像素直接丢弃（仍在不透明队列，正常写深度）。
    if (MaterialFlags.w > 0.5 && MaterialFlags.w < 1.5)
        clip(baseColor.a - AlphaParams.x);

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

    float3 albedo = baseColor.rgb;
    float2 mr = PbrParams.xy;
    if (TextureFlags.x > 0.5)
        mr *= MetallicRoughnessTexture.Sample(MetallicRoughnessSampler, input.Uv).bg;
    float metallic = saturate(mr.x);
    float roughness = clamp(mr.y, 0.05, 1.0);
    float3 viewDir = normalize(CameraPosition.xyz - input.WorldPosition);

    // 方向光（带 PCF 阴影）。
    float shadow = ComputeShadow(input);
    float3 lightDir = normalize(-LightDirection.xyz);
    float3 color = ShadeCookTorrance(normal, viewDir, lightDir, Diffuse.rgb * shadow, albedo, metallic, roughness);

    // 点光（无阴影）：smooth window 衰减，range 外为 0。
    int pointLightCount = min((int)PointLightMeta.x, MAX_POINT_LIGHTS);
    for (int i = 0; i < pointLightCount; i++)
    {
        float3 toLight = PointLightPositionRange[i].xyz - input.WorldPosition;
        float distance = length(toLight);
        float range = max(PointLightPositionRange[i].w, 0.001);
        float attenuation = saturate(1.0 - distance / range);
        attenuation *= attenuation;
        float3 pointRadiance = PointLightColorIntensity[i].rgb * PointLightColorIntensity[i].a * attenuation;
        color += ShadeCookTorrance(normal, viewDir, toLight / max(distance, 0.0001), pointRadiance, albedo, metallic, roughness);
    }

    // 环境光：无 IBL，平面环境项。
    // AO 只衰减间接光，不应遮挡直接光或自发光。
    float ao = TextureFlags.y > 0.5 ? lerp(1.0, OcclusionTexture.Sample(OcclusionSampler, input.Uv).r, Emissive.w) : 1.0;
    color += Ambient.rgb * albedo * ao;
    float3 emission = Emissive.xyz;
    if (TextureFlags.z > 0.5)
    {
        float3 sampleColor = EmissiveTexture.Sample(EmissiveSampler, input.Uv).rgb;
        if (ColorPipeline.x > 0.5 && TextureFlags.w < 0.5) sampleColor = SrgbToLinear(sampleColor);
        if (ColorPipeline.x < 0.5 && TextureFlags.w > 0.5) sampleColor = LinearToSrgb(sampleColor);
        emission *= sampleColor;
    }
    color += emission;
    return float4(ColorPipeline.x > 0.5 ? color : saturate(color), baseColor.a);
}
