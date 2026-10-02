#version 300 es
// Textured 片元着色器(GLSL ES 300, Web 后端), 语义同 Textured.hlsl
precision mediump float;

uniform sampler2D u_fragment_tex0;

in vec2 v_texcoord;
in vec4 v_color;

out vec4 o_color;

void main() {
	o_color = texture(u_fragment_tex0, v_texcoord) * v_color;
}
