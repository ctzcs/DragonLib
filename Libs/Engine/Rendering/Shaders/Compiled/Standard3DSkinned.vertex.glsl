#version 300 es
uniform float u_target_flip;

layout(std140) uniform VertexUniform0
{
    layout(row_major) mat4 WorldViewProjection;
    layout(row_major) mat4 World;
} VertexMatrixBlock;

layout(std140) uniform VertexUniform1
{
    layout(row_major) mat4 LightViewProjection;
} ShadowMatrixBlock;

layout(std140) uniform VertexUniform2
{
    layout(row_major) mat4 JointMatrices0[64];
} JointPaletteBlock0;

layout(std140) uniform VertexUniform3
{
    layout(row_major) mat4 JointMatrices1[64];
} JointPaletteBlock1;

layout(location = 0) in vec3 in_var_TEXCOORD0;
layout(location = 1) in vec3 in_var_TEXCOORD1;
layout(location = 2) in vec2 in_var_TEXCOORD2;
layout(location = 3) in vec4 in_var_TEXCOORD3;
layout(location = 4) in uvec4 in_var_TEXCOORD4;
layout(location = 5) in vec4 in_var_TEXCOORD5;
out vec3 v_loc0;
out vec4 v_loc1;
out vec2 v_loc2;
out vec4 v_loc3;
out vec3 v_loc4;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    uint _66 = (in_var_TEXCOORD4.x < 128u) ? in_var_TEXCOORD4.x : 0u;
    uint _70 = (in_var_TEXCOORD4.y < 128u) ? in_var_TEXCOORD4.y : 0u;
    uint _74 = (in_var_TEXCOORD4.z < 128u) ? in_var_TEXCOORD4.z : 0u;
    uint _78 = (in_var_TEXCOORD4.w < 128u) ? in_var_TEXCOORD4.w : 0u;
    mat4 _88;
    if (_66 < 64u)
    {
        _88 = spvWorkaroundRowMajor(JointPaletteBlock0.JointMatrices0[_66]);
    }
    else
    {
        _88 = spvWorkaroundRowMajor(JointPaletteBlock1.JointMatrices1[_66 - 64u]);
    }
    mat4 _91 = _88 * in_var_TEXCOORD5.x;
    mat4 _101;
    if (_70 < 64u)
    {
        _101 = spvWorkaroundRowMajor(JointPaletteBlock0.JointMatrices0[_70]);
    }
    else
    {
        _101 = spvWorkaroundRowMajor(JointPaletteBlock1.JointMatrices1[_70 - 64u]);
    }
    mat4 _104 = _101 * in_var_TEXCOORD5.y;
    mat4 _126;
    if (_74 < 64u)
    {
        _126 = spvWorkaroundRowMajor(JointPaletteBlock0.JointMatrices0[_74]);
    }
    else
    {
        _126 = spvWorkaroundRowMajor(JointPaletteBlock1.JointMatrices1[_74 - 64u]);
    }
    mat4 _129 = _126 * in_var_TEXCOORD5.z;
    mat4 _147;
    if (_78 < 64u)
    {
        _147 = spvWorkaroundRowMajor(JointPaletteBlock0.JointMatrices0[_78]);
    }
    else
    {
        _147 = spvWorkaroundRowMajor(JointPaletteBlock1.JointMatrices1[_78 - 64u]);
    }
    mat4 _150 = _147 * in_var_TEXCOORD5.w;
    vec4 _152 = ((_91[0] + _104[0]) + _129[0]) + _150[0];
    vec4 _154 = ((_91[1] + _104[1]) + _129[1]) + _150[1];
    vec4 _156 = ((_91[2] + _104[2]) + _129[2]) + _150[2];
    vec4 _164 = vec4(in_var_TEXCOORD0, 1.0) * mat4(_152, _154, _156, ((_91[3] + _104[3]) + _129[3]) + _150[3]);
    vec4 _167 = _164 * spvWorkaroundRowMajor(VertexMatrixBlock.World);
    mat3 _175 = mat3(_152.xyz, _154.xyz, _156.xyz);
    mat3 _183 = mat3(spvWorkaroundRowMajor(VertexMatrixBlock.World)[0].xyz, spvWorkaroundRowMajor(VertexMatrixBlock.World)[1].xyz, spvWorkaroundRowMajor(VertexMatrixBlock.World)[2].xyz);
    v_loc0 = normalize((in_var_TEXCOORD1 * _175) * _183);
    v_loc1 = vec4(normalize((in_var_TEXCOORD3.xyz * _175) * _183), in_var_TEXCOORD3.w);
    v_loc2 = in_var_TEXCOORD2;
    v_loc3 = _167 * spvWorkaroundRowMajor(ShadowMatrixBlock.LightViewProjection);
    v_loc4 = _167.xyz;
    gl_Position = _164 * spvWorkaroundRowMajor(VertexMatrixBlock.WorldViewProjection);
    gl_Position.z = 2.0 * clamp(gl_Position.z, 0.0, gl_Position.w) - gl_Position.w;
    gl_Position.y *= u_target_flip;
}
