#version 300 es
// Msdf 片元着色器(GLSL ES 300, Web 后端), 语义同 Msdf.hlsl
precision mediump float;

uniform sampler2D u_fragment_tex0;
layout(std140) uniform FragmentUniform0 { float u_distance_range; }; // 片元 uniform slot 0(DistanceRange)

in vec2 v_texcoord;
in vec4 v_color;

out vec4 o_color;

float median3(float x, float y, float z) {
	return max(min(x, y), min(max(x, y), z));
}

void main() {
	vec3 msd = texture(u_fragment_tex0, v_texcoord).rgb;
	vec2 size = 1.0 / fwidth(v_texcoord);
	vec2 unit = vec2(u_distance_range) / vec2(textureSize(u_fragment_tex0, 0));
	float value = max(0.5 * dot(unit, size), 1.0) * (median3(msd.r, msd.g, msd.b) - 0.5);
	o_color = v_color * clamp(value + 0.5, 0.0, 1.0);
}
