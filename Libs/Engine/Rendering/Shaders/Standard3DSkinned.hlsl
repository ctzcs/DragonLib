#include "Standard3DCommon.hlsli"
#define MAX_JOINTS 64

// joint palette：与 C# 侧 SkeletonAnimator.MaxJoints 对齐，由 Renderer3D 提交时写入（slot 2）。
cbuffer JointPaletteBlock : register(b2, space1)
{
    float4x4 JointMatrices[MAX_JOINTS];
};

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
    float4x4 skin =
        JointMatrices[input.Joints.x] * input.Weights.x +
        JointMatrices[input.Joints.y] * input.Weights.y +
        JointMatrices[input.Joints.z] * input.Weights.z +
        JointMatrices[input.Joints.w] * input.Weights.w;

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
