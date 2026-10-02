#version 300 es
// Batcher 片元着色器(GLSL ES 300, Web 后端)
// mode.x = Normal(tex * color), mode.y = Wash(color * tex.a), mode.z = Fill(color)
precision mediump float;

uniform sampler2D u_fragment_tex0;

in vec2 v_texcoord;
in vec4 v_color;
in vec4 v_mode;

out vec4 o_color;

void main() {
	vec4 tex = texture(u_fragment_tex0, v_texcoord);
	o_color = (tex * v_mode.x) * v_color
	        + v_color * (v_mode.y * tex.a)
	        + v_color * v_mode.z;
}
