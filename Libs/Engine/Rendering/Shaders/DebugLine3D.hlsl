cbuffer CameraBlock : register(b0, space1) { float4x4 ViewProjection; };
struct VsInput { float3 Position : TEXCOORD0; float4 Color : TEXCOORD1; };
struct VsOutput { float4 Position : SV_Position; float4 Color : TEXCOORD0; };
VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Position = mul(ViewProjection, float4(input.Position, 1));
    output.Color = input.Color;
    return output;
}
float4 fragment_main(VsOutput input) : SV_Target0 { return input.Color; }
