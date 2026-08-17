// A textureless fish. Every pixel is classified and coloured by this shader.
cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer FishVertexBlock : register(b1, space1)
{
    float4x4 CameraMatrix;
};

cbuffer FishBlock : register(b0, space3)
{
    float4 Params;      // x=time, y=length, z=body height, w=tail size
    float4 Direction;   // xy=normalised movement direction
    float4 Colors[3];   // base, stripe, belly
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

float softCoverage(float distanceToEdge)
{
    float aa = max(fwidth(distanceToEdge), 0.001);
    return 1.0 - smoothstep(-aa, aa, distanceToEdge);
}

float ellipseSdf(float2 p, float2 radius)
{
    return (length(p / radius) - 1.0) * min(radius.x, radius.y);
}

float triangleSdf(float2 p, float2 a, float2 b, float2 c)
{
    float2 e0 = b - a, v0 = p - a;
    float2 e1 = c - b, v1 = p - b;
    float2 e2 = a - c, v2 = p - c;
    float2 pq0 = v0 - e0 * saturate(dot(v0, e0) / dot(e0, e0));
    float2 pq1 = v1 - e1 * saturate(dot(v1, e1) / dot(e1, e1));
    float2 pq2 = v2 - e2 * saturate(dot(v2, e2) / dot(e2, e2));
    float s = sign(e0.x * e2.y - e0.y * e2.x);
    float2 d = min(min(float2(dot(pq0,pq0), s * (v0.x*e0.y-v0.y*e0.x)),
                       float2(dot(pq1,pq1), s * (v1.x*e1.y-v1.y*e1.x))),
                   float2(dot(pq2,pq2), s * (v2.x*e2.y-v2.y*e2.x)));
    return -sqrt(d.x) * sign(d.y);
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    // Convert quad UV to a local coordinate system centred on the fish.
    float2 p = (input.TexCoord - 0.5) * 12.0;
    float2 dir = normalize(Direction.xy);
    float2 perp = float2(-dir.y, dir.x);
    float along = dot(p, dir);
    float side = dot(p, perp);
    float2 local = float2(along, side);

    float bodyLength = Params.y;
    float bodyHeight = Params.z;
    float tailSize = Params.w;

    // Shape stage: SDF union of an ellipse (body) and a triangle (tail).
    float body = ellipseSdf(local - float2(0.35, 0.0), float2(bodyLength * 0.5, bodyHeight));
    float tailWag = sin(Params.x * 7.0) * 0.18;
    float tail = triangleSdf(local,
        float2(-bodyLength * 0.15, 0.0),
        float2(-bodyLength * 0.68, tailSize + tailWag),
        float2(-bodyLength * 0.68, -tailSize + tailWag));
    float fish = min(body, tail);
    float coverage = softCoverage(fish);
    if (coverage < 0.002)
        discard;

    // Material stage: stripes use longitudinal local coordinates, independent of rotation.
    float stripe = 0.5 + 0.5 * sin(along * 5.5 + side * 1.4);
    float3 color = lerp(Colors[0].rgb, Colors[1].rgb, smoothstep(0.64, 0.82, stripe));

    // Lighting stage: side is the fish's up/down coordinate, so the highlight rotates with it.
    float topLight = smoothstep(bodyHeight * 0.75, -bodyHeight * 0.75, side);
    color = lerp(color, Colors[2].rgb, smoothstep(0.1, 0.95, -side / bodyHeight) * 0.52);
    color *= 0.72 + topLight * 0.42;

    // Detail stage: an SDF eye is another small circle placed in local space.
    float eye = length(local - float2(bodyLength * 0.28, -bodyHeight * 0.22)) - bodyHeight * 0.14;
    color = lerp(color, float3(0.015, 0.02, 0.03), softCoverage(eye));
    return float4(color, coverage) * input.Color;
}
