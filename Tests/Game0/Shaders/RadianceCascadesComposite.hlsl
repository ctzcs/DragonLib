// Full-resolution composite for the texture-driven Radiance Cascades demo.
// The cascade chain runs at renderScale and produces only indirect radiance;
// this pass adds the crisp display terms (base color, emissive strokes and
// light stamps, scene shading) at window resolution, sampling the low-res
// cascade-0 texture bilinearly for the GI term — the same full-res scene /
// low-res lighting split the game integration uses.

cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer RadianceCascadesBlock : register(b0, space3)
{
    float4 Viewport;    // xy world size, z unused, w show scene
    float4 Cascade;     // unused here
    float4 Display;     // z sRGB, w GI floor (faint far-field radiance below this is crushed to zero)
    float4 WorldColor;  // rgb world color
    float4 Tuning;      // y GI intensity, z stroke intensity
    float4 Paint;       // x world width, y world height, z encode scale, w encode offset
};

// Each texture needs its OWN SamplerState: shadercross classifies textures
// that share one SamplerState as storage textures, which Foster never binds.
Texture2D Texture : register(t0, space2);
Texture2D StaticEmission : register(t1, space2);
Texture2D StaticDistance : register(t2, space2);
Texture2D DynamicEmission : register(t3, space2);
SamplerState Sampler : register(s0, space2);
SamplerState StaticEmissionSampler : register(s1, space2);
SamplerState StaticDistanceSampler : register(s2, space2);
SamplerState DynamicEmissionSampler : register(s3, space2);

struct VsInput
{
    float2 Position : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float4 Color : TEXCOORD2;
    float4 Mode : TEXCOORD4;
};

struct VsOutput
{
    float2 TexCoord : TEXCOORD0;
    float4 Color : TEXCOORD1;
    float4 Mode : TEXCOORD4;
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.Mode = input.Mode;
    output.Position = mul(Matrix, float4(input.Position, 0.0, 1.0));
    return output;
}

// The light stamp shader stores pow(radiance * 1/8, 1/2.2); decode inverts it.
static const float LightEmissionDecode = 8.0;

float2 fieldUv(float2 p)
{
    return p / Paint.xy + 0.5;
}

bool inField(float2 uv)
{
    return uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0;
}

float staticDistance(float2 p)
{
    float2 uv = fieldUv(p);
    if (!inField(uv))
        return Paint.z + Paint.w;
    return StaticDistance.Sample(StaticDistanceSampler, uv).r * Paint.z + Paint.w;
}

float3 staticEmission(float2 p)
{
    float4 s = StaticEmission.Sample(StaticEmissionSampler, fieldUv(p));
    return pow(s.rgb, 2.2) * s.a * Tuning.z;
}

float3 dynamicEmission(float2 p)
{
    float3 s = DynamicEmission.Sample(DynamicEmissionSampler, fieldUv(p)).rgb;
    return pow(s, 2.2) * LightEmissionDecode;
}

float2 fieldNormal(float2 p)
{
    float e = 0.012;
    return normalize(float2(staticDistance(p + float2(e, 0.0)) - staticDistance(p - float2(e, 0.0)),
                             staticDistance(p + float2(0.0, e)) - staticDistance(p - float2(0.0, e))));
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float2 uv = input.TexCoord;
    float2 worldPosition = (uv - 0.5) * Viewport.xy;
    float3 indirect = Texture.Sample(Sampler, uv).rgb;
    // De-dirt the GI term: soft-knee rolloff crushes faint far-field haze to
    // zero (no contour line where the haze ends), so dark backgrounds stay
    // clean, then scale what remains.
    indirect = indirect * indirect / max(indirect + Display.w, 1e-5) * Tuning.y;

    float scene = staticDistance(worldPosition);
    float4 stroke = StaticEmission.Sample(StaticEmissionSampler, fieldUv(worldPosition));
    float3 strokeEmission = pow(stroke.rgb, 2.2) * stroke.a * Tuning.z;
    float3 base = WorldColor.rgb * (0.62 + 0.38 * (1.0 - uv.y));
    // Painted pixels shine at full strength; nearby falloff arrives
    // through the cascade transport instead of an analytic halo.
    float strokeGlow = exp(-max(scene, 0.0) * 1.6);
    float3 lightGlow = strokeEmission * (0.3 + 0.7 * strokeGlow);
    lightGlow += dynamicEmission(worldPosition) * 1.1;
    float3 color = indirect + base + lightGlow;

    // Strokes share the merged distance field with scene geometry; don't
    // shade their interiors as dark geometry or each stroke gets a dark rim.
    if (Viewport.w > 0.5 && scene < 0.0 && stroke.a < 0.05)
    {
        float2 normal = fieldNormal(worldPosition);
        float topLight = 0.35 + 0.65 * saturate(dot(normal, normalize(float2(-0.5, -0.8))));
        color = WorldColor.rgb * topLight + indirect * 0.68;
        color += float3(0.025, 0.03, 0.045);
    }

    if (Display.z > 0.5)
        color = pow(max(color, 0.0), 1.0 / 2.2);
    return float4(saturate(color), 1.0) * input.Color;
}
