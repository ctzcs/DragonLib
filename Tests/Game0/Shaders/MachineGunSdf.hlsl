// A procedural, textureless machine gun assembled from signed-distance primitives.
cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer GunVertexBlock : register(b1, space1)
{
    float4x4 CameraMatrix;
};

cbuffer GunBlock : register(b0, space3)
{
    float4 Params;      // x=time, y=scale, z=fire intensity, w=recoil
    float4 Direction;   // xy=aim direction
    float4 MetalColor;
    float4 GripColor;
    float4 AccentColor;
    float4 GlowColor;
    float4 PanelColor;
    float4 BrassColor;
};

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

float sdRoundBox(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
}

float sdSegment(float2 p, float2 a, float2 b, float radius)
{
    float2 ab = b - a;
    float h = saturate(dot(p - a, ab) / dot(ab, ab));
    return length(p - a - ab * h) - radius;
}

float2 rotate(float2 p, float angle)
{
    float c = cos(angle), s = sin(angle);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

float coverage(float d)
{
    float aa = max(fwidth(d), 0.002);
    return 1.0 - smoothstep(-aa, aa, d);
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    float2 p = (input.TexCoord - 0.5) * 15.0;
    float2 aim = normalize(Direction.xy);
    float2 side = float2(-aim.y, aim.x);
    float2 q = float2(dot(p, aim), dot(p, side)) / Params.y;

    // Shape layer: a long sci-fi heavy weapon, built from simple SDF parts.
    float recoil = Params.w * 0.18;
    float upperHousing = sdRoundBox(q - float2(-0.25 + recoil, 0.52), float2(2.35, 0.82), 0.12);
    float lowerHousing = sdRoundBox(q - float2(-0.10 + recoil, -0.62), float2(1.80, 0.48), 0.13);
    float frontStrut = sdRoundBox(q - float2(-2.46 + recoil, 0.12), float2(0.20, 1.05), 0.05);
    float barrelTop = sdSegment(q, float2(-2.50 + recoil, 0.31), float2(-5.18 + recoil, 0.31), 0.105);
    float barrelLow = sdSegment(q, float2(-2.50 + recoil, -0.03), float2(-5.02 + recoil, -0.03), 0.13);
    float muzzle = sdRoundBox(q - float2(-5.13 + recoil, 0.14), float2(0.26, 0.46), 0.06);
    float barrelShroud = sdRoundBox(q - float2(-3.24 + recoil, 0.13), float2(0.88, 0.43), 0.09);
    float lowerPod = sdRoundBox(q - float2(-0.36 + recoil, -1.18), float2(1.10, 0.42), 0.09);
    float grip = sdRoundBox(rotate(q - float2(-0.18, -1.50), -0.22), float2(0.35, 0.74), 0.13);
    float rearBridge = sdSegment(q, float2(1.78, 0.22), float2(2.66, -0.12), 0.30);
    float drum = length(q - float2(2.83, -0.31)) - 0.83;
    float rearBlock = sdRoundBox(q - float2(3.72, 0.14), float2(0.55, 0.73), 0.12);
    float rearForkTop = sdSegment(q, float2(3.10, 0.48), float2(4.24, 0.78), 0.22);
    float rearForkLow = sdSegment(q, float2(3.18, -0.64), float2(4.15, -0.34), 0.20);
    float sight = sdRoundBox(q - float2(-0.72, 1.45), float2(0.72, 0.13), 0.03);

    float gun = min(upperHousing, min(lowerHousing, min(frontStrut, min(barrelTop, min(barrelLow,
        min(muzzle, min(barrelShroud, min(lowerPod, min(grip, min(rearBridge, min(drum,
        min(rearBlock, min(rearForkTop, min(rearForkLow, sight))))))))))))));

    // The fire effect is another SDF union, rather than a particle texture.
    float fire = sdSegment(q, float2(-5.50 + recoil, 0.14), float2(-5.85 - Params.z * 1.20, 0.14), 0.16 + Params.z * 0.28);
    float shape = min(gun, fire);
    float alpha = coverage(shape);
    if (alpha < 0.002)
        discard;

    // Material layer: closest component chooses its base material.
    float darkParts = min(min(lowerPod, grip), min(rearBlock, barrelShroud));
    float3 color = lerp(MetalColor.rgb, GripColor.rgb, coverage(darkParts));
    float receiverPanel = coverage(sdRoundBox(q - float2(-0.18 + recoil, 0.44), float2(1.10, 0.34), 0.05));
    float drumInner = coverage(length(q - float2(2.83, -0.31)) - 0.58);
    color = lerp(color, GripColor.rgb, receiverPanel * 0.56);
    color = lerp(color, GripColor.rgb, drumInner * 0.82);

    // Armour plates break the large silhouette into readable mechanical layers.
    float frontPlate = coverage(sdRoundBox(q - float2(-1.45 + recoil, 0.62), float2(0.62, 0.48), 0.07));
    float centrePlate = coverage(sdRoundBox(q - float2(0.78 + recoil, 0.61), float2(0.55, 0.46), 0.06));
    float rearPlate = coverage(sdRoundBox(q - float2(3.70, 0.18), float2(0.38, 0.52), 0.06));
    float drumRim = coverage(length(q - float2(2.83, -0.31)) - 0.82) - coverage(length(q - float2(2.83, -0.31)) - 0.61);
    color = lerp(color, PanelColor.rgb, saturate(frontPlate + centrePlate + rearPlate + drumRim));

    // A dark reactor window with animated internal coils.
    float reactorWindow = coverage(sdRoundBox(q - float2(-0.18 + recoil, 0.66), float2(0.72, 0.25), 0.11));
    float coils = 0.5 + 0.5 * sin((q.x + 0.18 - recoil) * 18.0);
    color = lerp(color, GripColor.rgb, reactorWindow);
    color = lerp(color, GlowColor.rgb, reactorWindow * coils * (0.55 + 0.45 * sin(Params.x * 5.0)));

    // Detail and lighting layer.
    float vents = 0.5 + 0.5 * sin((q.x + 3.28) * 21.0);
    float ventBand = coverage(sdRoundBox(q - float2(-3.24 + recoil, 0.13), float2(0.62, 0.17), 0.02));
    color *= 1.0 - vents * ventBand * 0.55;
    float cyanRail = coverage(sdSegment(q, float2(-2.05 + recoil, 1.08), float2(1.18 + recoil, 1.08), 0.042));
    float pipeA = coverage(sdSegment(q, float2(-1.22, 0.12), float2(-0.66, -0.55), 0.065));
    float pipeB = coverage(sdSegment(q, float2(-0.66, -0.55), float2(0.62, -0.55), 0.065));
    float pipeC = coverage(sdSegment(q, float2(0.62, -0.55), float2(0.92, 0.25), 0.065));
    float cable = coverage(sdSegment(q, float2(1.16, 0.24), float2(2.22, 0.06), 0.09));
    float drumCore = coverage(length(q - float2(2.83, -0.31)) - 0.30) * (0.62 + 0.38 * sin(Params.x * 6.0));
    color = lerp(color, GlowColor.rgb, max(cyanRail, drumCore) * 0.94);
    color = lerp(color, BrassColor.rgb, max(max(pipeA, pipeB), pipeC) * 0.96);
    color = lerp(color, BrassColor.rgb, cable * 0.72);

    // Warning stripe, panel seams and fasteners add the small-scale mecha language.
    float warningBand = coverage(sdRoundBox(q - float2(0.12 + recoil, -0.86), float2(0.88, 0.12), 0.02));
    float warningPattern = step(0.5, frac((q.x + q.y * 0.7) * 3.5));
    color = lerp(color, lerp(GripColor.rgb, AccentColor.rgb, warningPattern), warningBand * 0.92);
    float seamA = coverage(sdSegment(q, float2(-0.82 + recoil, -0.06), float2(-0.82 + recoil, 1.08), 0.022));
    float seamB = coverage(sdSegment(q, float2(1.38 + recoil, -0.04), float2(1.38 + recoil, 1.00), 0.022));
    color = lerp(color, GripColor.rgb, max(seamA, seamB) * 0.82);
    float boltA = coverage(length(q - float2(-1.83 + recoil, 0.24)) - 0.075);
    float boltB = coverage(length(q - float2(1.53 + recoil, 0.38)) - 0.075);
    float boltC = coverage(length(q - float2(3.69, 0.28)) - 0.075);
    color = lerp(color, BrassColor.rgb, saturate(boltA + boltB + boltC));
    float topLight = smoothstep(-1.75, 1.55, q.y);
    color *= 0.48 + topLight * 0.72;
    float hardHighlight = smoothstep(0.55, 0.92, topLight) * coverage(gun + 0.05);
    color += PanelColor.rgb * hardHighlight * 0.28;
    float muzzleHole = coverage(length(q - float2(-5.18 + recoil, 0.14)) - 0.12);
    color = lerp(color, float3(0.012, 0.015, 0.02), muzzleHole);

    float edgeGlow = exp(-max(gun, 0.0) * 10.0) * 0.16;
    float fireMask = coverage(fire) * saturate(Params.z * 2.2);
    float3 fireColor = lerp(float3(1.0, 0.16, 0.02), float3(1.0, 0.92, 0.38), saturate(1.0 - abs(q.y) * 3.5));
    color = lerp(color, fireColor, fireMask);
    color += GlowColor.rgb * edgeGlow;
    return float4(color, max(alpha, edgeGlow)) * input.Color;
}
