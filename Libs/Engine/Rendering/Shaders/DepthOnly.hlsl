// Depth-only pass for the directional light shadow map.
// Vertex layout matches any mesh whose position is at TEXCOORD0
// (PositionNormalColorVertex / PositionNormalUvVertex / glTF meshes).

cbuffer ShadowVertexBlock : register(b0, space1)
{
    float4x4 WorldLightViewProjection;
};

struct VsInput
{
    float3 Position : TEXCOORD0;
};

struct VsOutput
{
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    output.Position = mul(WorldLightViewProjection, float4(input.Position, 1.0));
    return output;
}

// Shadow target carries an unused color attachment (Foster requires one);
// the depth buffer is what the main pass samples.
float4 fragment_main() : SV_Target0
{
    return 0;
}
