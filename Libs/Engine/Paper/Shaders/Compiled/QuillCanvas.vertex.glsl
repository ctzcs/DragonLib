#version 300 es
uniform float u_target_flip;

layout(std140) uniform VertexUniform0
{
    layout(row_major) mat4 Matrix;
} VertexUniformBlock;

layout(location = 0) in vec2 in_var_TEXCOORD0;
layout(location = 1) in vec2 in_var_TEXCOORD1;
layout(location = 2) in vec4 in_var_TEXCOORD2;
out vec2 v_loc0;
out vec4 v_loc1;
out vec2 v_loc2;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    v_loc0 = in_var_TEXCOORD1;
    v_loc1 = in_var_TEXCOORD2;
    v_loc2 = in_var_TEXCOORD0;
    gl_Position = vec4(in_var_TEXCOORD0, 0.0, 1.0) * spvWorkaroundRowMajor(VertexUniformBlock.Matrix);
    gl_Position.z = 2.0 * clamp(gl_Position.z, 0.0, gl_Position.w) - gl_Position.w;
    gl_Position.y *= u_target_flip;
}
