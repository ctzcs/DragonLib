// Depth-only pass for the directional light shadow map.
// Vertex layout matches any mesh whose position is at TEXCOORD0
// (PositionNormalColorVertex / PositionNormalUvVertex / glTF meshes).

cbuffer ShadowVertexBlock : register(b0, space1)
{
    float4x4 WorldLightViewProjection;
};

#define MAX_JOINTS 64

// joint palette：与 C# 侧 SkeletonAnimator.MaxJoints 对齐，由 Renderer3D 提交时写入（slot 2）。
cbuffer JointPaletteBlock : register(b2, space1)
{
    float4x4 JointMatrices[MAX_JOINTS];
};

struct VsInput
{
    // 深度变体省掉 normal/uv/tangent，显式 location 防止 SPIR-V 把关节压缩到槽 1/2。
    [[vk::location(0)]] float3 Position : TEXCOORD0;
    [[vk::location(4)]] uint4 Joints : TEXCOORD4;
    [[vk::location(5)]] float4 Weights : TEXCOORD5;
};

struct VsOutput
{
    float4 Position : SV_Position;
};

VsOutput vertex_main(VsInput input)
{
    VsOutput output;
    float4x4 skin =
        JointMatrices[input.Joints.x] * input.Weights.x +
        JointMatrices[input.Joints.y] * input.Weights.y +
        JointMatrices[input.Joints.z] * input.Weights.z +
        JointMatrices[input.Joints.w] * input.Weights.w;
    output.Position = mul(WorldLightViewProjection, mul(skin, float4(input.Position, 1.0)));
    return output;
}

// Shadow target carries an unused color attachment (Foster requires one);
// the depth buffer is what the main pass samples.
float4 fragment_main() : SV_Target0
{
    return 0;
}
