#version 300 es
// Batcher 顶点着色器(GLSL ES 300, Web 后端)
// 语义与 assets/shaders 下的 SPIR-V/DXIL/MSL 版本一致(spirv-cross 反编译校对):
//   gl_Position = Matrix * vec4(pos, 0, 1);  Matrix 为列主序 64 字节
precision highp float;

layout(std140) uniform VertexUniform0 { mat4 u_matrix; };
uniform float u_target_flip;

layout(location = 0) in vec2 a_position;
layout(location = 1) in vec2 a_texcoord;
layout(location = 2) in vec4 a_color;
layout(location = 3) in vec4 a_mode;

out vec2 v_texcoord;
out vec4 v_color;
out vec4 v_mode;

void main() {
	v_texcoord = a_texcoord;
	v_color = a_color;
	v_mode = a_mode;
	gl_Position = u_matrix * vec4(a_position, 0.0, 1.0);
    gl_Position.y *= u_target_flip;
}
