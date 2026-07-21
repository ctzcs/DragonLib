cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer DreamBlockVertexBlock : register(b1, space1)
{
    float4x4 VertexCameraMatrix;
    float4 VertexAnimation;
    float4 VertexShape;
    float4 VertexEffect;
    float4 VertexImpact0;
    float4 VertexImpact1;
    float4 VertexImpact2;
    float4 VertexImpact3;
};

cbuffer DreamBlockFragmentBlock : register(b0, space3)
{
    float4 DeepColor;
    float4 MidColor;
    float4 EdgeColor;
    float4 Animation;
    float4 Shape;
    float4 Effect;
    float4 Impact0;
    float4 Impact1;
    float4 Impact2;
    float4 Impact3;
};

struct VsOutput
{
    float2 TexCoord : TEXCOORD0;
    float4 Color : TEXCOORD1;
    float4 Type : TEXCOORD4;
    float4 Position : SV_Position;
};

struct VsInput
{
    float2 Position : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float4 Color : TEXCOORD2;
    float4 Type : TEXCOORD4;
};

float hash21(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

float valueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    float2 curve = local * local * (3.0 - 2.0 * local);

    float a = hash21(cell);
    float b = hash21(cell + float2(1.0, 0.0));
    float c = hash21(cell + float2(0.0, 1.0));
    float d = hash21(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, curve.x), lerp(c, d, curve.x), curve.y);
}

float roundedBoxSdf(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
}

float impactWave(float2 p, float4 impact, float duration)
{
    if (impact.w <= 0.0 || impact.z < 0.0 || impact.z > duration)
        return 0.0;

    float distanceFromImpact = distance(p, impact.xy);
    float life = saturate(1.0 - impact.z / duration);
    float wave = sin(distanceFromImpact * 8.5 - impact.z * 17.0);
    float spatialFalloff = exp(-distanceFromImpact * 0.55);
    return wave * life * life * spatialFalloff * impact.w;
}

float combinedImpactWave(
    float2 p,
    float duration,
    float4 impact0,
    float4 impact1,
    float4 impact2,
    float4 impact3)
{
    return
        impactWave(p, impact0, duration) +
        impactWave(p, impact1, duration) +
        impactWave(p, impact2, duration) +
        impactWave(p, impact3, duration);
}

float edgeWobble(float2 p, float time)
{
    return
        sin(p.x * 2.1 + time * 2.3) * 0.55 +
        sin(p.y * 2.7 - time * 1.7) * 0.30 +
        sin((p.x + p.y) * 1.15 + time * 1.1) * 0.15;
}

float2 roundedBoxNormal(float2 p, float2 halfSize, float radius)
{
    const float epsilon = 0.01;
    float2 gradient = float2(
        roundedBoxSdf(p + float2(epsilon, 0.0), halfSize, radius) -
            roundedBoxSdf(p - float2(epsilon, 0.0), halfSize, radius),
        roundedBoxSdf(p + float2(0.0, epsilon), halfSize, radius) -
            roundedBoxSdf(p - float2(0.0, epsilon), halfSize, radius));
    return gradient / max(length(gradient), 0.0001);
}

VsOutput vertex_main(VsInput input)
{
    float time = VertexAnimation.x;
    float warpAmount = VertexAnimation.z;
    float2 outerSize = VertexShape.xy;
    float2 halfSize = VertexShape.zw;
    float cornerRadius = min(VertexEffect.x, min(halfSize.x, halfSize.y) - 0.001);
    float rippleStrength = VertexEffect.z;
    float impactDuration = VertexEffect.w;

    float2 local = (input.TexCoord - 0.5) * outerSize;
    float baseDistance = roundedBoxSdf(local, halfSize, cornerRadius);
    float edgeInfluence = exp(-abs(baseDistance) * 1.8);
    float ripples = combinedImpactWave(
        local,
        impactDuration,
        VertexImpact0,
        VertexImpact1,
        VertexImpact2,
        VertexImpact3);
    float deformation = edgeWobble(local, time) * warpAmount + ripples * rippleStrength;
    float2 normal = roundedBoxNormal(local, halfSize, cornerRadius);

    float2 worldPosition = input.Position + normal * deformation * edgeInfluence;
    float4 screenPosition = mul(VertexCameraMatrix, float4(worldPosition, 0.0, 1.0));

    VsOutput output;
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.Type = input.Type;
    output.Position = mul(Matrix, screenPosition);
    return output;
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float time = Animation.x;
    float flowSpeed = Animation.y;
    float warpAmount = Animation.z;
    float edgeWidth = max(Animation.w, 0.001);
    float2 outerSize = Shape.xy;
    float2 halfSize = Shape.zw;
    float cornerRadius = min(Effect.x, min(halfSize.x, halfSize.y) - 0.001);
    float glowIntensity = Effect.y;
    float rippleStrength = Effect.z;
    float impactDuration = Effect.w;

    float2 local = (input.TexCoord - 0.5) * outerSize;
    float baseDistance = roundedBoxSdf(local, halfSize, cornerRadius);

    float edgeMask = exp(-abs(baseDistance) * 2.0);
    float wobble = edgeWobble(local, time);
    float ripples = combinedImpactWave(
        local,
        impactDuration,
        Impact0,
        Impact1,
        Impact2,
        Impact3);

    float distanceField = baseDistance - edgeMask *
        (wobble * warpAmount * 0.25 + ripples * rippleStrength * 0.18);
    float antialias = max(fwidth(distanceField), 0.001);
    float fillCoverage = 1.0 - smoothstep(-antialias, antialias, distanceField);
    float border = 1.0 - smoothstep(edgeWidth * 0.2, edgeWidth, abs(distanceField));

    float2 flowPosition = local * 0.72;
    float noiseA = valueNoise(flowPosition + float2(time * 0.22 * flowSpeed, -time * 0.16 * flowSpeed));
    float noiseB = valueNoise(flowPosition * 1.9 + float2(-time * 0.31 * flowSpeed, time * 0.20 * flowSpeed));
    float bands = 0.5 + 0.5 * sin(local.y * 1.25 + local.x * 0.42 + time * 1.6 * flowSpeed + noiseB * 2.2);
    float flowMix = saturate(noiseA * 0.55 + bands * 0.45);
    float3 fillColor = lerp(DeepColor.rgb, MidColor.rgb, flowMix);

    float2 starGrid = local * 2.2;
    float2 starCell = floor(starGrid);
    float2 starPoint = float2(hash21(starCell), hash21(starCell + 19.17));
    float starDistance = length(frac(starGrid) - starPoint);
    float starSeed = hash21(starCell + 7.31);
    float star = step(0.91, starSeed) * (1.0 - smoothstep(0.025, 0.12, starDistance));
    star *= 0.55 + 0.45 * sin(time * (1.8 + starSeed * 2.0) + starSeed * 13.0);
    fillColor += EdgeColor.rgb * max(star, 0.0) * 0.85;

    float3 interior = lerp(fillColor, EdgeColor.rgb, border * 0.82);
    float outsideDistance = max(distanceField, 0.0);
    float glow = exp(-outsideDistance * 3.2) * (1.0 - fillCoverage) * glowIntensity * 0.42;
    float alpha = saturate(fillCoverage + glow);
    float3 color = interior * fillCoverage + EdgeColor.rgb * glow;

    return float4(color, alpha) * input.Color;
}
