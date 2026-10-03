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

layout(location = 0) in vec3 in_var_TEXCOORD0;
layout(location = 1) in vec3 in_var_TEXCOORD1;
layout(location = 2) in vec2 in_var_TEXCOORD2;
layout(location = 3) in vec4 in_var_TEXCOORD3;
out vec3 v_loc0;
out vec4 v_loc1;
out vec2 v_loc2;
out vec4 v_loc3;
out vec3 v_loc4;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    vec4 _50 = vec4(in_var_TEXCOORD0, 1.0);
    mat3 _60 = mat3(spvWorkaroundRowMajor(VertexMatrixBlock.World)[0].xyz, spvWorkaroundRowMajor(VertexMatrixBlock.World)[1].xyz, spvWorkaroundRowMajor(VertexMatrixBlock.World)[2].xyz);
    vec4 _72 = _50 * spvWorkaroundRowMajor(VertexMatrixBlock.World);
    v_loc0 = normalize(in_var_TEXCOORD1 * _60);
    v_loc1 = vec4(normalize(in_var_TEXCOORD3.xyz * _60), in_var_TEXCOORD3.w);
    v_loc2 = in_var_TEXCOORD2;
    v_loc3 = vec4(_72.xyz, 1.0) * spvWorkaroundRowMajor(ShadowMatrixBlock.LightViewProjection);
    v_loc4 = _72.xyz;
    gl_Position = _50 * spvWorkaroundRowMajor(VertexMatrixBlock.WorldViewProjection);
    gl_Position.z = 2.0 * clamp(gl_Position.z, 0.0, gl_Position.w) - gl_Position.w;
    gl_Position.y *= u_target_flip;
}
