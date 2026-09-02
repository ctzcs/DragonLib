// Separable 9-tap gaussian blur for the low-res cascade-0 (indirect radiance)
// texture. Runs at cascade resolution, so smoothing the GI term before the
// full-res composite is nearly free. Direction.xy is the texel step:
// (1/width, 0) for the horizontal pass, (0, 1/height) for vertical.
// Offsets/weights are the linear-sampling-optimized gaussian (5 fetches
// instead of 9), so the bound sampler must be linear.

cbuffer VertexMatrixBlock : register(b0, space1)
{
    float4x4 Matrix;
};

cbuffer BlurBlock : register(b0, space3)
{
    float4 Direction;
};

Texture2D Texture : register(t0, space2);
SamplerState Sampler : register(s0, space2);

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

float4 fragment_main(VsOutput input) : SV_Target0
{
    float2 uv = input.TexCoord;
    float2 offset1 = Direction.xy * 1.3846153846;
    float2 offset2 = Direction.xy * 3.2307692308;
    float3 color = Texture.Sample(Sampler, uv).rgb * 0.2270270270;
    color += Texture.Sample(Sampler, uv + offset1).rgb * 0.3162162162;
    color += Texture.Sample(Sampler, uv - offset1).rgb * 0.3162162162;
    color += Texture.Sample(Sampler, uv + offset2).rgb * 0.0702702703;
    color += Texture.Sample(Sampler, uv - offset2).rgb * 0.0702702703;
    return float4(color, 1.0) * input.Color;
}
