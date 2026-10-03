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
    layout(row_major) mat4 JointMatrices[64];
} JointPaletteBlock;

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
    mat4 _63 = spvWorkaroundRowMajor(JointPaletteBlock.JointMatrices[in_var_TEXCOORD4.x]) * in_var_TEXCOORD5.x;
    mat4 _70 = spvWorkaroundRowMajor(JointPaletteBlock.JointMatrices[in_var_TEXCOORD4.y]) * in_var_TEXCOORD5.y;
    mat4 _89 = spvWorkaroundRowMajor(JointPaletteBlock.JointMatrices[in_var_TEXCOORD4.z]) * in_var_TEXCOORD5.z;
    mat4 _104 = spvWorkaroundRowMajor(JointPaletteBlock.JointMatrices[in_var_TEXCOORD4.w]) * in_var_TEXCOORD5.w;
    vec4 _106 = ((_63[0] + _70[0]) + _89[0]) + _104[0];
    vec4 _108 = ((_63[1] + _70[1]) + _89[1]) + _104[1];
    vec4 _110 = ((_63[2] + _70[2]) + _89[2]) + _104[2];
    vec4 _118 = vec4(in_var_TEXCOORD0, 1.0) * mat4(_106, _108, _110, ((_63[3] + _70[3]) + _89[3]) + _104[3]);
    vec4 _121 = _118 * spvWorkaroundRowMajor(VertexMatrixBlock.World);
    mat3 _129 = mat3(_106.xyz, _108.xyz, _110.xyz);
    mat3 _137 = mat3(spvWorkaroundRowMajor(VertexMatrixBlock.World)[0].xyz, spvWorkaroundRowMajor(VertexMatrixBlock.World)[1].xyz, spvWorkaroundRowMajor(VertexMatrixBlock.World)[2].xyz);
    v_loc0 = normalize((in_var_TEXCOORD1 * _129) * _137);
    v_loc1 = vec4(normalize((in_var_TEXCOORD3.xyz * _129) * _137), in_var_TEXCOORD3.w);
    v_loc2 = in_var_TEXCOORD2;
    v_loc3 = _121 * spvWorkaroundRowMajor(ShadowMatrixBlock.LightViewProjection);
    v_loc4 = _121.xyz;
    gl_Position = _118 * spvWorkaroundRowMajor(VertexMatrixBlock.WorldViewProjection);
    gl_Position.z = 2.0 * clamp(gl_Position.z, 0.0, gl_Position.w) - gl_Position.w;
    gl_Position.y *= u_target_flip;
}
