// 静态与蒙皮共用片元管线；每张贴图必须独占 sampler，避免 shadercross 错判 storage texture。
#include "EnvironmentUv.hlsli"
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
    float4 Environment; // enabled, intensity, rotation, level count
    float4 EnvironmentSize; // width, height per roughness level, atlas height
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
    float4x4 CascadeMatrices[4];
    float4 CascadeSplits;
    float4 CascadeSettings; // count、blend fraction、debug colors
    float4 ShadowCameraForward;
};

#define MAX_POINT_LIGHTS 16
#define MAX_SPOT_LIGHTS 16

// 与 C# 侧 PointLight3D.Pack 的布局一致。
cbuffer Standard3DPointLightBlock : register(b3, space3)
{
    float4 PointLightMeta; // x: count
    float4 PointLightPositionRange[MAX_POINT_LIGHTS];  // xyz: position, w: range
    float4 PointLightColorIntensity[MAX_POINT_LIGHTS]; // xyz: color, w: intensity
    float4 SpotLightMeta;
    float4 SpotLightPositionRange[MAX_SPOT_LIGHTS];
    float4 SpotLightColorIntensity[MAX_SPOT_LIGHTS];
    float4 SpotLightDirectionOuter[MAX_SPOT_LIGHTS]; // xyz: outgoing direction, w: cos outer half-angle
    float4 SpotLightInner[MAX_SPOT_LIGHTS]; // x: cos inner half-angle
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
Texture2D EnvironmentDiffuse : register(t6, space2);
SamplerState EnvironmentDiffuseSampler : register(s6, space2);
Texture2D EnvironmentSpecular : register(t7, space2);
SamplerState EnvironmentSpecularSampler : register(s7, space2);
Texture2D EnvironmentBrdf : register(t8, space2);
SamplerState EnvironmentBrdfSampler : register(s8, space2);

struct VsOutput
{
    float3 Normal : TEXCOORD0;
    float4 Tangent : TEXCOORD1;
    float2 Uv : TEXCOORD2;
    float4 ShadowPosition : TEXCOORD3;
    float3 WorldPosition : TEXCOORD4;
    float4 Position : SV_Position;
};

int SelectCascade(float depth)
{
    return depth <= CascadeSplits.x ? 0 : depth <= CascadeSplits.y ? 1 : depth <= CascadeSplits.z ? 2 : 3;
}

float SampleShadow(float4 position, int cascade, bool atlas)
{
    float3 ndc = position.xyz / position.w;
    // 深度附件与颜色附件都用左上角纹理坐标；NDC 的 y 朝上，因此这里要反转。
    float2 uv = float2(ndc.x, -ndc.y) * 0.5 + 0.5;
    if (any(uv < 0) || any(uv > 1) || ndc.z < 0 || ndc.z > 1) return 1;
    float2 lo = atlas ? float2(cascade % 2, cascade / 2) * 0.5 : float2(0, 0);
    float2 hi = lo + (atlas ? 0.5 : 1.0);
    uv = atlas ? lo + uv * 0.5 : uv;
    float lit = 0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            // clamp 到当前 tile，PCF 不跨级联污染相邻深度。
            float2 sampleUv = clamp(uv + float2(x, y) * ShadowSettings.y,
                lo + ShadowSettings.y * 0.5, hi - ShadowSettings.y * 0.5);
            float depth = ShadowMapTexture.Sample(ShadowSampler, sampleUv).r;
            lit += ndc.z - ShadowSettings.z <= depth ? 1.0 : 0.0;
        }
    return 1.0 - ShadowSettings.w * (1.0 - lit / 9.0);
}

float ComputeShadow(VsOutput input)
{
    if (ShadowSettings.x < 0.5) return 1;
    if (CascadeSettings.x < 0.5) return SampleShadow(input.ShadowPosition, 0, false);
    float depth = dot(input.WorldPosition - CameraPosition.xyz, ShadowCameraForward.xyz);
    if (depth > CascadeSplits.w) return 1;
    int cascade = SelectCascade(depth);
    float value = SampleShadow(mul(CascadeMatrices[cascade], float4(input.WorldPosition, 1)), cascade, true);
    if (cascade < 3 && CascadeSettings.y > 0)
    {
        float start = cascade == 0 ? ShadowCameraForward.w : CascadeSplits[cascade - 1];
        float width = max((CascadeSplits[cascade] - start) * CascadeSettings.y, 0.0001);
        float blend = saturate((depth - CascadeSplits[cascade] + width) / width);
        if (blend > 0) value = lerp(value,
            SampleShadow(mul(CascadeMatrices[cascade + 1], float4(input.WorldPosition, 1)), cascade + 1, true), blend);
    }
    return value;
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

    int spotLightCount = min((int)SpotLightMeta.x, MAX_SPOT_LIGHTS);
    for (int j = 0; j < spotLightCount; j++)
    {
        float3 toLight = SpotLightPositionRange[j].xyz - input.WorldPosition;
        float distance = length(toLight);
        float3 direction = toLight / max(distance, .0001);
        float cosine = dot(-direction, SpotLightDirectionOuter[j].xyz);
        float outer = SpotLightDirectionOuter[j].w;
        float inner = SpotLightInner[j].x;
        float cone = inner - outer > .00001 ? saturate((cosine - outer) / (inner - outer)) : step(outer, cosine);
        cone = cone * cone * (3 - 2 * cone);
        float attenuation = saturate(1 - distance / max(SpotLightPositionRange[j].w, .001));
        float3 radiance = SpotLightColorIntensity[j].rgb * SpotLightColorIntensity[j].w * attenuation * attenuation * cone;
        color += ShadeCookTorrance(normal, viewDir, direction, radiance, albedo, metallic, roughness);
    }

    // 环境贴图为线性数据；GGX roughness 层单独加 padding，不能跨层过滤。
    // AO 只衰减间接光，不应遮挡直接光或自发光。
    float ao = TextureFlags.y > 0.5 ? lerp(1.0, OcclusionTexture.Sample(OcclusionSampler, input.Uv).r, Emissive.w) : 1.0;
    color += Ambient.rgb * albedo * ao;
    if (Environment.x > .5)
    {
        float nv = saturate(dot(normal, viewDir));
        float3 f0 = lerp(float3(.04, .04, .04), ColorPipeline.x > .5 ? albedo : SrgbToLinear(albedo), metallic);
        float3 fresnel = f0 + (max(float3(1 - roughness, 1 - roughness, 1 - roughness), f0) - f0) * pow(1 - nv, 5);
        float3 reflected = RotateEnvironment(reflect(-viewDir, normal), Environment.z);
        float2 uv = EnvironmentUv(reflected);
        float level = roughness * (Environment.w - 1);
        float lo = floor(level), hi = min(lo + 1, Environment.w - 1);
        float2 atlasLo = float2(uv.x, (lo * (EnvironmentSize.y + 2) + 1.5 + uv.y * (EnvironmentSize.y - 1)) / EnvironmentSize.z);
        float2 atlasHi = float2(uv.x, (hi * (EnvironmentSize.y + 2) + 1.5 + uv.y * (EnvironmentSize.y - 1)) / EnvironmentSize.z);
        float3 prefiltered = lerp(EnvironmentSpecular.Sample(EnvironmentSpecularSampler, atlasLo).rgb,
            EnvironmentSpecular.Sample(EnvironmentSpecularSampler, atlasHi).rgb, level - lo);
        float2 brdf = EnvironmentBrdf.Sample(EnvironmentBrdfSampler, float2(nv, roughness)).rg;
        float3 irradiance = EnvironmentDiffuse.Sample(EnvironmentDiffuseSampler, EnvironmentUv(RotateEnvironment(normal, Environment.z))).rgb;
        float3 indirect = ((1 - fresnel) * (1 - metallic) * (ColorPipeline.x > .5 ? albedo : SrgbToLinear(albedo)) * irradiance +
            prefiltered * (f0 * brdf.x + brdf.y)) * Environment.y * ao;
        color += ColorPipeline.x > .5 ? indirect : LinearToSrgb(indirect);
    }
    float3 emission = Emissive.xyz;
    if (TextureFlags.z > 0.5)
    {
        float3 sampleColor = EmissiveTexture.Sample(EmissiveSampler, input.Uv).rgb;
        if (ColorPipeline.x > 0.5 && TextureFlags.w < 0.5) sampleColor = SrgbToLinear(sampleColor);
        if (ColorPipeline.x < 0.5 && TextureFlags.w > 0.5) sampleColor = LinearToSrgb(sampleColor);
        emission *= sampleColor;
    }
    color += emission;
    if (CascadeSettings.z > 0.5 && CascadeSettings.x > 0.5)
    {
        float depth = dot(input.WorldPosition - CameraPosition.xyz, ShadowCameraForward.xyz);
        int cascade = SelectCascade(depth);
        float3 tint = cascade == 0 ? float3(1, .2, .2) : cascade == 1 ? float3(.2, 1, .2)
            : cascade == 2 ? float3(.2, .2, 1) : float3(1, 1, .2);
        color = lerp(color, tint, .4);
    }
    return float4(ColorPipeline.x > 0.5 ? color : saturate(color), baseColor.a);
}
