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
    float4 WorldColor;
    float4 Tuning;     // x interval overlap, y light count, z emission segment count, w raymarch steps
    float4 Obstacle0;
    float4 Obstacle1;
    float4 Obstacle2;
    float4 Obstacle3;
    float4 Obstacle4;
    float4 Obstacle5;
    float4 Obstacle6;
    float4 Obstacle7;
};

struct LightGpuData
{
    float4 Position; // xy position, z intensity, w radius
    float4 Color;    // rgb color, a enabled
};

struct EmissionGpuData
{
    float4 Segment; // xy start, zw end
    float4 Color;   // rgb color * intensity, a radius
};

StructuredBuffer<LightGpuData> Lights : register(t1, space2);
StructuredBuffer<EmissionGpuData> Emissions : register(t2, space2);

Texture2D Texture : register(t0, space2);
SamplerState Sampler : register(s0, space2);

struct VsInput
{
    float2 Position : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float4 Color : TEXCOORD2;
    float4 Type : TEXCOORD4;
};

struct VsOutput
{
    float2 TexCoord : TEXCOORD0;
    float4 Color : TEXCOORD1;
    float4 Type : TEXCOORD4;
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.Type = input.Type;
    output.Position = mul(Matrix, mul(CameraMatrix, float4(input.Position, 0.0, 1.0)));
    return output;
}

static const float TAU = 6.28318530718;

float sdRoundBox(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
}

float sdSegment(float2 p, float2 a, float2 b, float radius)
{
    float2 ab = b - a;
    float h = saturate(dot(p - a, ab) / max(dot(ab, ab), 0.0001));
    return length(p - a - ab * h) - radius;
}

float obstacleDistance(float2 p, float4 segment)
{
    return sdSegment(p, segment.xy, segment.zw, 0.11);
}

float sceneDistance(float2 p)
{
    float d = 1000.0;
    d = min(d, sdRoundBox(p - float2(1.9, 2.9), float2(1.8, 0.34), 0.16));
    d = min(d, sdRoundBox(p - float2(7.2, 2.9), float2(0.34, 2.3), 0.16));
    d = min(d, length(p - float2(4.8, -3.2)) - 1.05);
    d = min(d, sdRoundBox(p - float2(-5.6, 5.0), float2(2.5, 0.32), 0.12));
    if (WorldColor.a > 0.5) d = min(d, obstacleDistance(p, Obstacle0));
    if (WorldColor.a > 1.5) d = min(d, obstacleDistance(p, Obstacle1));
    if (WorldColor.a > 2.5) d = min(d, obstacleDistance(p, Obstacle2));
    if (WorldColor.a > 3.5) d = min(d, obstacleDistance(p, Obstacle3));
    if (WorldColor.a > 4.5) d = min(d, obstacleDistance(p, Obstacle4));
    if (WorldColor.a > 5.5) d = min(d, obstacleDistance(p, Obstacle5));
    if (WorldColor.a > 6.5) d = min(d, obstacleDistance(p, Obstacle6));
    if (WorldColor.a > 7.5) d = min(d, obstacleDistance(p, Obstacle7));
    return d;
}

float2 sceneNormal(float2 p)
{
    float e = 0.012;
    return normalize(float2(sceneDistance(p + float2(e, 0.0)) - sceneDistance(p - float2(e, 0.0)),
                             sceneDistance(p + float2(0.0, e)) - sceneDistance(p - float2(0.0, e))));
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
        float3 lightRadiance = float3(0.0, 0.0, 0.0);
        float lightHit = 0.0;
        for (int segmentIndex = 0; segmentIndex < (int)Tuning.z; segmentIndex++)
        {
            float segmentRadius = max(Emissions[segmentIndex].Color.a, 0.01);
            float segmentDistance = sdSegment(samplePosition,
                Emissions[segmentIndex].Segment.xy,
                Emissions[segmentIndex].Segment.zw,
                segmentRadius);
            if (segmentDistance < 0.0)
            {
                float glow = smoothstep(segmentRadius, 0.0, max(segmentDistance + segmentRadius, 0.0));
                lightRadiance += Emissions[segmentIndex].Color.rgb * (0.55 + 0.45 * glow);
                lightHit = 1.0;
            }
        }
        for (int lightIndex = 0; lightIndex < (int)Tuning.y; lightIndex++)
        {
            if (Lights[lightIndex].Color.a < 0.5)
                continue;
            float2 toLight = Lights[lightIndex].Position.xy - samplePosition;
            float lightDistance = length(toLight);
            float lightRadius = max(Lights[lightIndex].Position.w * 1.6, 0.08);
            if (lightDistance < lightRadius)
            {
                float facing = saturate(dot(direction, normalize(toLight + float2(0.0001, 0.0001))));
                float glow = smoothstep(lightRadius, 0.0, lightDistance);
                lightRadiance += Lights[lightIndex].Color.rgb * Lights[lightIndex].Position.z * (0.45 + 0.55 * facing) * glow;
                lightHit = 1.0;
            }
        }
        if (lightHit > 0.5)
        {
            result.rgb += lightRadiance;
            result.a = 1.0;
            break;
        }

        float distanceToScene = sceneDistance(samplePosition);
        if (distanceToScene < 0.018)
        {
            float2 normal = sceneNormal(samplePosition);
            for (int lightIndex = 0; lightIndex < (int)Tuning.y; lightIndex++)
            {
                if (Lights[lightIndex].Color.a < 0.5)
                    continue;
                float2 toSource = Lights[lightIndex].Position.xy - samplePosition;
                float sourceDistance = length(toSource);
                float lambert = saturate(dot(normal, toSource / max(sourceDistance, 0.001)));
                float bounce = lambert * Lights[lightIndex].Position.z / (1.0 + sourceDistance * sourceDistance * 0.18);
                result.rgb += Lights[lightIndex].Color.rgb * bounce * 0.24;
            }
            result.a = 1.0;
            break;
        }

        // Emission strokes are not part of sceneDistance. Stepping only by the
        // scene SDF leaps over these thin strokes in open space, so the rays
        // never register the light the user drew. Fold each stroke's SDF into
        // the march step so rays decelerate onto the stroke and illuminate it.
        float marchDistance = distanceToScene;
        for (int marchSegmentIndex = 0; marchSegmentIndex < (int)Tuning.z; marchSegmentIndex++)
        {
            float segmentRadius = max(Emissions[marchSegmentIndex].Color.a, 0.01);
            marchDistance = min(marchDistance, sdSegment(samplePosition,
                Emissions[marchSegmentIndex].Segment.xy,
                Emissions[marchSegmentIndex].Segment.zw,
                segmentRadius));
        }
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
    float scene = sceneDistance(worldPosition);
    float hitAmount;
    float3 indirect = radiancePass(worldPosition, uv, hitAmount);
    float3 color = indirect;

    // Only the lowest cascade becomes the final display layer. Upper layers
    // remain linear radiance textures so they can be merged by the next pass.
    if ((int)Display.x == 0)
    {
        float3 base = WorldColor.rgb * (0.62 + 0.38 * (1.0 - uv.y));
        float3 lightGlow = float3(0.0, 0.0, 0.0);
        for (int segmentIndex = 0; segmentIndex < (int)Tuning.z; segmentIndex++)
        {
            float segmentRadius = max(Emissions[segmentIndex].Color.a, 0.01);
            float segmentDistance = sdSegment(worldPosition,
                Emissions[segmentIndex].Segment.xy,
                Emissions[segmentIndex].Segment.zw,
                segmentRadius);
            float glow = smoothstep(segmentRadius * 8.0, 0.0, max(segmentDistance, 0.0));
            lightGlow += Emissions[segmentIndex].Color.rgb * glow * 0.26;
        }
        for (int lightIndex = 0; lightIndex < (int)Tuning.y; lightIndex++)
        {
            if (Lights[lightIndex].Color.a < 0.5)
                continue;
            lightGlow += Lights[lightIndex].Color.rgb * Lights[lightIndex].Position.z * exp(-length(worldPosition - Lights[lightIndex].Position.xy) * 0.72) * 0.12;
        }
        color += base + lightGlow;
    }
    // Scene presentation belongs only to the final (highest spatial
    // resolution) cascade. Intermediate cascades must remain pure radiance so
    // their packed probes can be merged without duplicating UI/display terms.
    if ((int)Display.x == 0)
    {
        for (int lightIndex = 0; lightIndex < (int)Tuning.y; lightIndex++)
        {
            if (Lights[lightIndex].Color.a < 0.5)
                continue;
            float lightDisc = smoothstep(
                max(Lights[lightIndex].Position.w * 1.45, 0.12),
                max(Lights[lightIndex].Position.w * 0.18, 0.025),
                length(worldPosition - Lights[lightIndex].Position.xy));
            color = lerp(color, Lights[lightIndex].Color.rgb * (1.0 + Lights[lightIndex].Position.z * 0.18), lightDisc);
        }

        if (Viewport.w > 0.5 && scene < 0.0)
        {
            float2 normal = sceneNormal(worldPosition);
            float topLight = 0.35 + 0.65 * saturate(dot(normal, normalize(float2(-0.5, -0.8))));
            color = WorldColor.rgb * topLight + indirect * 0.68;
            color += float3(0.025, 0.03, 0.045);
        }
    }

    if (Display.w > 0.5)
    {
        float2 probeGrid = floor(uv * screenResolution() / max(pow(sqrt((float)Cascade.x), (float)max((int)Display.x, 0)), 1.0));
        float2 gridLines = abs(frac(probeGrid) - 0.5);
        float gridLineStrength = 1.0 - smoothstep(0.44, 0.50, max(gridLines.x, gridLines.y));
        color = lerp(color, float3(0.15, 0.55, 0.75), gridLineStrength * 0.35);
    }

    if ((int)Display.x == 0 && Display.z > 0.5)
        color = pow(max(color, 0.0), 1.0 / 2.2);
    return float4(saturate(color), 1.0) * input.Color;
}
