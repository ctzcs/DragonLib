// Depth-only pass for the directional light shadow map.
// Vertex layout matches any mesh whose position is at TEXCOORD0
// (PositionNormalColorVertex / PositionNormalUvVertex / glTF meshes).

cbuffer ShadowVertexBlock : register(b0, space1)
{
    float4x4 WorldLightViewProjection;
};

#define MAX_JOINTS 128

// SDL 3.4 Vulkan 每槽 descriptor range 只有 4KB；分成两块，128 关节不能放一个 8KB UBO。
cbuffer JointPaletteBlock0 : register(b2, space1) { float4x4 JointMatrices0[64]; };
cbuffer JointPaletteBlock1 : register(b3, space1) { float4x4 JointMatrices1[64]; };
float4x4 JointMatrix(uint index)
{
    return index < 64 ? JointMatrices0[index] : JointMatrices1[index - 64];
}

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
    // 旧资产和超限骨架都可能含越界下标；即使权重为零，也不能越界读取 cbuffer。
    uint4 joints = uint4(input.Joints.x < MAX_JOINTS ? input.Joints.x : 0,
        input.Joints.y < MAX_JOINTS ? input.Joints.y : 0,
        input.Joints.z < MAX_JOINTS ? input.Joints.z : 0,
        input.Joints.w < MAX_JOINTS ? input.Joints.w : 0);
    float4x4 skin =
        JointMatrix(joints.x) * input.Weights.x +
        JointMatrix(joints.y) * input.Weights.y +
        JointMatrix(joints.z) * input.Weights.z +
        JointMatrix(joints.w) * input.Weights.w;
    output.Position = mul(WorldLightViewProjection, mul(skin, float4(input.Position, 1.0)));
    return output;
}

// Shadow target carries an unused color attachment (Foster requires one);
// the depth buffer is what the main pass samples.
float4 fragment_main() : SV_Target0
{
    return 0;
}
