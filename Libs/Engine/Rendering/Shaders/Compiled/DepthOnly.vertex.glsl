#version 300 es
uniform float u_target_flip;

layout(std140) uniform VertexUniform0
{
    layout(row_major) mat4 WorldLightViewProjection;
} ShadowVertexBlock;

layout(location = 0) in vec3 in_var_TEXCOORD0;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    gl_Position = vec4(in_var_TEXCOORD0, 1.0) * spvWorkaroundRowMajor(ShadowVertexBlock.WorldLightViewProjection);
    gl_Position.z = 2.0 * clamp(gl_Position.z, 0.0, gl_Position.w) - gl_Position.w;
    gl_Position.y *= u_target_flip;
}
