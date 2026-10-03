#version 300 es
uniform float u_target_flip;

layout(std140) uniform VertexUniform0
{
    layout(row_major) mat4 WorldLightViewProjection;
} ShadowVertexBlock;

layout(std140) uniform VertexUniform2
{
    layout(row_major) mat4 JointMatrices0[64];
} JointPaletteBlock0;

layout(std140) uniform VertexUniform3
{
    layout(row_major) mat4 JointMatrices1[64];
} JointPaletteBlock1;

layout(location = 0) in vec3 in_var_TEXCOORD0;
layout(location = 4) in uvec4 in_var_TEXCOORD4;
layout(location = 5) in vec4 in_var_TEXCOORD5;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    uint _46 = (in_var_TEXCOORD4.x < 128u) ? in_var_TEXCOORD4.x : 0u;
    uint _50 = (in_var_TEXCOORD4.y < 128u) ? in_var_TEXCOORD4.y : 0u;
    uint _54 = (in_var_TEXCOORD4.z < 128u) ? in_var_TEXCOORD4.z : 0u;
    uint _58 = (in_var_TEXCOORD4.w < 128u) ? in_var_TEXCOORD4.w : 0u;
    mat4 _68;
    if (_46 < 64u)
    {
        _68 = spvWorkaroundRowMajor(JointPaletteBlock0.JointMatrices0[_46]);
    }
    else
    {
        _68 = spvWorkaroundRowMajor(JointPaletteBlock1.JointMatrices1[_46 - 64u]);
    }
    mat4 _71 = _68 * in_var_TEXCOORD5.x;
    mat4 _81;
    if (_50 < 64u)
    {
        _81 = spvWorkaroundRowMajor(JointPaletteBlock0.JointMatrices0[_50]);
    }
    else
    {
        _81 = spvWorkaroundRowMajor(JointPaletteBlock1.JointMatrices1[_50 - 64u]);
    }
    mat4 _84 = _81 * in_var_TEXCOORD5.y;
    mat4 _106;
    if (_54 < 64u)
    {
        _106 = spvWorkaroundRowMajor(JointPaletteBlock0.JointMatrices0[_54]);
    }
    else
    {
        _106 = spvWorkaroundRowMajor(JointPaletteBlock1.JointMatrices1[_54 - 64u]);
    }
    mat4 _109 = _106 * in_var_TEXCOORD5.z;
    mat4 _127;
    if (_58 < 64u)
    {
        _127 = spvWorkaroundRowMajor(JointPaletteBlock0.JointMatrices0[_58]);
    }
    else
    {
        _127 = spvWorkaroundRowMajor(JointPaletteBlock1.JointMatrices1[_58 - 64u]);
    }
    mat4 _130 = _127 * in_var_TEXCOORD5.w;
    gl_Position = (vec4(in_var_TEXCOORD0, 1.0) * mat4(((_71[0] + _84[0]) + _109[0]) + _130[0], ((_71[1] + _84[1]) + _109[1]) + _130[1], ((_71[2] + _84[2]) + _109[2]) + _130[2], ((_71[3] + _84[3]) + _109[3]) + _130[3])) * spvWorkaroundRowMajor(ShadowVertexBlock.WorldLightViewProjection);
    gl_Position.z = 2.0 * clamp(gl_Position.z, 0.0, gl_Position.w) - gl_Position.w;
    gl_Position.y *= u_target_flip;
}
