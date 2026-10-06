cbuffer MatrixBlock : register(b0, space1) { float4x4 Matrix; };
struct VsInput { float2 Position : TEXCOORD0; float2 Uv : TEXCOORD1; };
struct VsOutput { float2 Uv : TEXCOORD0; float4 Position : SV_Position; };
VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Uv = input.Uv;
    output.Position = mul(Matrix, float4(input.Position, 0, 1));
    return output;
}
float3 EncodeSrgb(float3 x)
{
    return float3(x.x <= .0031308 ? x.x * 12.92 : 1.055 * pow(max(x.x, 0), 1 / 2.4) - .055,
                  x.y <= .0031308 ? x.y * 12.92 : 1.055 * pow(max(x.y, 0), 1 / 2.4) - .055,
                  x.z <= .0031308 ? x.z * 12.92 : 1.055 * pow(max(x.z, 0), 1 / 2.4) - .055);
}
float3 DecodeSrgb(float3 x)
{
    return float3(x.x <= .04045 ? x.x / 12.92 : pow((x.x + .055) / 1.055, 2.4),
                  x.y <= .04045 ? x.y / 12.92 : pow((x.y + .055) / 1.055, 2.4),
                  x.z <= .04045 ? x.z / 12.92 : pow((x.z + .055) / 1.055, 2.4));
}
