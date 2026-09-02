// Texture-driven 2D Radiance Cascades. Scene geometry, painted strokes and
// point lights are all baked into world-anchored textures, so the raymarch
// inner loop is two texture fetches regardless of light count or geometry
// complexity:
//   StaticEmission  - painted strokes (sRGB color + coverage alpha)
//   StaticDistance  - encoded min distance to scene geometry AND strokes
//   DynamicEmission - point-light radial stamps (pow-encoded radiance x 1/8)
//   DynamicDistance - encoded distance to light discs
// The probe packing, interval cascade and ping-pong merge steps follow the
// reference sandbox, unchanged from the analytic version.

cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer RadianceVertexBlock : register(b1, space1)
{
    float4x4 CameraMatrix;
};

cbuffer RadianceCascadesBlock : register(b0, space3)
{
    float4 Viewport;    // xy world size, z pixel width, w show scene
    float4 Cascade;     // x base ray count, y cascade count, z interval split, w max distance
    float4 Display;     // x selected cascade (-1 final), y linear filter, z sRGB, w probe overlay
    float4 WorldColor;  // rgb world color
    float4 Tuning;      // x interval overlap, y unused, z stroke intensity, w raymarch steps
    float4 Paint;       // x world width, y world height, z encode scale, w encode offset
};

// Each texture needs its OWN SamplerState: shadercross classifies textures
// that share one SamplerState as storage textures, which Foster never binds.
Texture2D Texture : register(t0, space2);
Texture2D StaticEmission : register(t1, space2);
Texture2D StaticDistance : register(t2, space2);
Texture2D DynamicEmission : register(t3, space2);
Texture2D DynamicDistance : register(t4, space2);
SamplerState Sampler : register(s0, space2);
SamplerState StaticEmissionSampler : register(s1, space2);
SamplerState StaticDistanceSampler : register(s2, space2);
SamplerState DynamicEmissionSampler : register(s3, space2);
SamplerState DynamicDistanceSampler : register(s4, space2);

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
    output.Position = mul(Matrix, mul(CameraMatrix, float4(input.Position, 0.0, 1.0)));
    return output;
}

static const float TAU = 6.28318530718;
// The light stamp shader stores pow(radiance * 1/8, 1/2.2); decode inverts it,
// giving 8x HDR headroom in an 8-bit target.
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
        return Paint.z + Paint.w; // outside the baked region reads as far
    return StaticDistance.Sample(StaticDistanceSampler, uv).r * Paint.z + Paint.w;
}

float dynamicDistance(float2 p)
{
    float2 uv = fieldUv(p);
    if (!inField(uv))
        return Paint.z + Paint.w;
    return DynamicDistance.Sample(DynamicDistanceSampler, uv).r * Paint.z + Paint.w;
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

float4 traceRay(float2 origin, float2 direction, float intervalStart, float intervalEnd)
{
    float travel = max(intervalStart, 0.02);
    float4 result = float4(0.0, 0.0, 0.0, 0.0);
    [loop]
    for (int stepIndex = 0; stepIndex < 24; stepIndex++)
    {
        if (stepIndex >= (int)Tuning.w)
            break;
        if (travel >= intervalEnd)
            break;

        float2 samplePosition = origin + direction * travel;

        // Static hit: a painted stroke (emissive) or scene geometry (dark, so
        // it acts purely as an occluder for the cascade transport).
        float dStatic = staticDistance(samplePosition);
        if (dStatic < 0.025)
        {
            result.rgb += staticEmission(samplePosition);
            result.a = 1.0;
            break;
        }

        // Dynamic hit: a point-light disc; the stamp's radial falloff bakes
        // the glow profile, so nearer rays pick up more radiance.
        float dDynamic = dynamicDistance(samplePosition);
        if (dDynamic < 0.025)
        {
            result.rgb += dynamicEmission(samplePosition);
            result.a = 1.0;
            break;
        }

        float marchDistance = min(dStatic, dDynamic);
        travel += max(marchDistance, 0.018);
    }
    return result;
}

float2 screenResolution()
{
    return float2(Viewport.z, max(Viewport.z * Viewport.y / max(Viewport.x, 0.001), 1.0));
}

float3 radiancePass(float2 worldPosition, float2 uv, out float hitAmount)
{
    int baseRays = (int)Cascade.x;
    int cascadeIndex = (int)Display.x;
    int cascadeCount = (int)Cascade.y;
    float2 resolution = screenResolution();
    float2 coord = uv * resolution;
    float sqrtBase = sqrt((float)baseRays);
    float spacing = pow(sqrtBase, (float)cascadeIndex);
    float2 probeSize = max(floor(resolution / max(spacing, 1.0)), float2(1.0, 1.0));
    float2 probeRelative = fmod(coord, probeSize);
    float2 rayPosition = floor(coord / probeSize);
    float baseIndex = (float)baseRays * (rayPosition.x + spacing * rayPosition.y);
    float2 probeCenter = (probeRelative + 0.5) * spacing;
    float2 origin = (probeCenter / resolution - 0.5) * Viewport.xy;
    float rayCount = pow((float)baseRays, (float)(cascadeIndex + 1));
    // Match the final reference shader's interval construction, including its
    // small overlap term and the base-16 smoothing modifier.
    float pixelWorldSize = min(Viewport.x / max(resolution.x, 1.0), Viewport.y / max(resolution.y, 1.0));
    float modifier = baseRays < 16 ? 1.0 : sqrtBase;
    float modifiedInterval = modifier * max(Cascade.z, 0.05);
    float previousPower = pow((float)baseRays, (float)(cascadeIndex - 1));
    float intervalStart = (cascadeIndex == 0 ? 1.0 : modifiedInterval) * previousPower * pixelWorldSize;
    float intervalLength = modifiedInterval * (
        (pow((float)baseRays, (float)cascadeIndex) - previousPower) +
        Tuning.x * pow((float)baseRays, (float)(cascadeIndex + 1)) / 1.41421356) * pixelWorldSize;
    float intervalEnd = cascadeIndex == cascadeCount - 1
        ? Cascade.w
        : min(Cascade.w, intervalStart + intervalLength);
    float3 radiance = float3(0.0, 0.0, 0.0);
    hitAmount = 0.0;

    [loop]
    for (int rayIndex = 0; rayIndex < 16; rayIndex++)
    {
        if (rayIndex >= baseRays)
            break;
        float rayIndexValue = baseIndex + rayIndex;
        float angleIndex = rayIndexValue + 0.5;
        float angle = angleIndex * TAU / max(rayCount, 1.0);
        float2 direction = float2(cos(angle), -sin(angle));
        float4 rayResult = traceRay(origin, direction, intervalStart, intervalEnd);
        radiance += rayResult.rgb;
        hitAmount += rayResult.a;

        // Decode the upper cascade's packed probe. This is the merge step
        // that connects the lower-resolution, higher-angular-resolution pass.
        if (cascadeIndex < cascadeCount - 1 && rayResult.a < 0.5)
        {
            float upperSpacing = pow(sqrtBase, (float)(cascadeIndex + 1));
            float2 upperSize = max(floor(resolution / upperSpacing), float2(1.0, 1.0));
            float2 upperPosition = float2(fmod(rayIndexValue, upperSpacing), floor(rayIndexValue / upperSpacing)) * upperSize;
            float2 offset = (probeRelative + 0.5) / sqrtBase;
            float2 clamped = clamp(offset, float2(0.5, 0.5), upperSize - 0.5);
            float2 upperUv = (upperPosition + clamped) / resolution;
            // Upper cascade textures are stored as linear radiance already.
            radiance += Texture.Sample(Sampler, saturate(upperUv)).rgb;
        }
    }
    hitAmount /= max((float)baseRays, 1.0);
    return radiance / max((float)baseRays, 1.0);
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float2 uv = input.TexCoord;
    float2 worldPosition = (uv - 0.5) * Viewport.xy;
    float hitAmount;
    // Every cascade now stays pure linear radiance; the display terms live in
    // the full-resolution composite pass (RadianceCascadesComposite.hlsl).
    float3 color = radiancePass(worldPosition, uv, hitAmount);

    if (Display.w > 0.5)
    {
        float2 probeGrid = floor(uv * screenResolution() / max(pow(sqrt((float)Cascade.x), (float)max((int)Display.x, 0)), 1.0));
        float2 gridLines = abs(frac(probeGrid) - 0.5);
        float gridLineStrength = 1.0 - smoothstep(0.44, 0.50, max(gridLines.x, gridLines.y));
        color = lerp(color, float3(0.15, 0.55, 0.75), gridLineStrength * 0.35);
    }

    return float4(saturate(color), 1.0) * input.Color;
}
