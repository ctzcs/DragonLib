#version 300 es
// Textured 顶点着色器(GLSL ES 300, Web 后端), 语义同 Textured.hlsl
precision highp float;

layout(std140) uniform VertexUniform0 { mat4 u_matrix; };
uniform float u_target_flip;

layout(location = 0) in vec2 a_position;
layout(location = 1) in vec2 a_texcoord;
layout(location = 2) in vec4 a_color;

out vec2 v_texcoord;
out vec4 v_color;

void main() {
	v_texcoord = a_texcoord;
	v_color = a_color;
	gl_Position = u_matrix * vec4(a_position, 0.0, 1.0);
    gl_Position.y *= u_target_flip;
}
