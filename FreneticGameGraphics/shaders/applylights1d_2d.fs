#version 430 core

layout (binding = 0) uniform sampler1D tex;

layout (location = 3) uniform vec2 l_scaler;
layout (location = 4) uniform vec2 l_adder;
layout (location = 5) uniform float light_width;
layout (location = 6) uniform vec4 light_color;
layout (location = 7) uniform float aspect;
layout (location = 8) uniform vec2 light_pos = vec2(0.0);

layout (location = 1) in vec2 f_texcoord;
layout (location = 2) in vec2 f_pos;

out vec4 color;

void main()
{
	vec2 lmat = (f_texcoord * vec2(2.0) + vec2(-1.0)) * l_scaler + l_adder;
	lmat.y /= aspect;
	vec2 mmat = lmat;
	float modif = max(0.95 - dot(mmat, mmat), 0.0);
	if (modif < 0.01)
	{
		discard;
	}
	vec2 rel_pos = f_pos - light_pos;
	float ownDist = dot(rel_pos, rel_pos);
	float xDist = texture(tex, atan(rel_pos.y, rel_pos.x) * 100.0 + 320.0).x;
	modif *= ownDist >= xDist ? 0.2 : 1.0;
	vec4 c = light_color * modif;
	color = vec4(c.xyz * c.xyz, c.w);
}
