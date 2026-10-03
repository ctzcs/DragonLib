#include "Standard3DCommon.hlsli"
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
    float3 Position : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 Uv : TEXCOORD2;
    float4 Tangent : TEXCOORD3;
    uint4 Joints : TEXCOORD4;
    float4 Weights : TEXCOORD5;
};

VsOutput vertex_main(VsInput input)
{
    // LBS：蒙皮矩阵加权和（权重和为 1，cook 时已归一化），实体 World 照常乘在后面。
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

    VsOutput output;
    float4 skinned = mul(skin, float4(input.Position, 1.0));
    float4 worldPosition = mul(World, skinned);
    output.Position = mul(WorldViewProjection, skinned);
    output.WorldPosition = worldPosition.xyz;
    // 法线/切线：先蒙皮后实体 World 的 3x3（MVP 不做逆转置，非均匀缩放/剪切下有轻微误差，
    // 与静态路径的 normal 处理精度一致）。
    float3 skinnedNormal = mul((float3x3)skin, input.Normal);
    output.Normal = normalize(mul((float3x3)World, skinnedNormal));
    float3 skinnedTangent = mul((float3x3)skin, input.Tangent.xyz);
    output.Tangent = float4(normalize(mul((float3x3)World, skinnedTangent)), input.Tangent.w);
    output.Uv = input.Uv;
    output.ShadowPosition = mul(LightViewProjection, worldPosition);
    return output;
}
