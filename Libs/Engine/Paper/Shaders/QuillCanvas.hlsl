cbuffer VertexUniformBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer FragmentUniformBlock : register(b0, space3)
{
    float4x4 BrushTextureMatrix;
    float DistanceRange;
    float DpiScale;
    float2 FragmentPadding;
};

Texture2D BrushTexture : register(t0, space2);
SamplerState BrushSampler : register(s0, space2);
Texture2D FontTexture : register(t1, space2);
SamplerState FontSampler : register(s1, space2);

struct VsInput
{
    float2 Position : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float4 Color : TEXCOORD2;
};

struct VsOutput
{
    float2 TexCoord : TEXCOORD0;
    float4 Color : TEXCOORD1;
    float2 FragmentPosition : TEXCOORD2;
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.FragmentPosition = input.Position;
    output.Position = mul(Matrix, float4(input.Position, 0.0, 1.0));
    return output;
}

float sdfScreenPxRange(float2 uv)
{
    float2 textureSize;
    FontTexture.GetDimensions(textureSize.x, textureSize.y);
    float2 unitRange = DistanceRange / textureSize;
    float2 screenTextureSize = 1.0 / max(fwidth(uv), float2(0.000001, 0.000001));
    return max(0.5 * dot(unitRange, screenTextureSize), 1.0);
}

float4 fragment_main(VsOutput input) : SV_Target0
{
    // Quill/Scribe reserves UV >= 2 for text. Keep that marker intact until here.
    if (input.TexCoord.x >= 2.0)
    {
        float2 uv = input.TexCoord - float2(2.0, 2.0);
        float signedDistance = FontTexture.Sample(FontSampler, uv).r;
        float screenDistance = sdfScreenPxRange(uv) * (signedDistance - 0.5);
        float coverage = saturate(screenDistance + 0.5);
        return input.Color * coverage;
    }

    // Quill stores vector-geometry fringe coverage in TexCoord.x.
    float edgeCoverage = saturate(input.TexCoord.x);
    float2 logicalPosition = input.FragmentPosition / max(DpiScale, 0.000001);
    float2 brushUv = mul(BrushTextureMatrix, float4(logicalPosition, 0.0, 1.0)).xy;
    float4 fill = input.Color * BrushTexture.Sample(BrushSampler, brushUv);
    return fill * edgeCoverage;
}
