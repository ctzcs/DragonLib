#version 300 es
uniform float u_target_flip;

layout(std140) uniform VertexUniform0
{
    layout(row_major) mat4 WorldLightViewProjection;
} ShadowVertexBlock;

layout(std140) uniform VertexUniform2
{
    layout(row_major) mat4 JointMatrices[64];
} JointPaletteBlock;

layout(location = 0) in vec3 in_var_TEXCOORD0;
layout(location = 4) in uvec4 in_var_TEXCOORD4;
layout(location = 5) in vec4 in_var_TEXCOORD5;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    mat4 _43 = spvWorkaroundRowMajor(JointPaletteBlock.JointMatrices[in_var_TEXCOORD4.x]) * in_var_TEXCOORD5.x;
    mat4 _50 = spvWorkaroundRowMajor(JointPaletteBlock.JointMatrices[in_var_TEXCOORD4.y]) * in_var_TEXCOORD5.y;
    mat4 _69 = spvWorkaroundRowMajor(JointPaletteBlock.JointMatrices[in_var_TEXCOORD4.z]) * in_var_TEXCOORD5.z;
    mat4 _84 = spvWorkaroundRowMajor(JointPaletteBlock.JointMatrices[in_var_TEXCOORD4.w]) * in_var_TEXCOORD5.w;
    gl_Position = (vec4(in_var_TEXCOORD0, 1.0) * mat4(((_43[0] + _50[0]) + _69[0]) + _84[0], ((_43[1] + _50[1]) + _69[1]) + _84[1], ((_43[2] + _50[2]) + _69[2]) + _84[2], ((_43[3] + _50[3]) + _69[3]) + _84[3])) * spvWorkaroundRowMajor(ShadowVertexBlock.WorldLightViewProjection);
    gl_Position.z = 2.0 * clamp(gl_Position.z, 0.0, gl_Position.w) - gl_Position.w;
    gl_Position.y *= u_target_flip;
}
