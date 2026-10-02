#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    layout(row_major) highp mat4 BrushTextureMatrix;
    highp float DistanceRange;
    highp float DpiScale;
    highp float TextAntialiasWidth;
    highp float FragmentPadding;
} FragmentUniformBlock;

uniform highp sampler2D u_fragment_tex1;
uniform highp sampler2D u_fragment_tex0;

in highp vec2 v_loc0;
in highp vec4 v_loc1;
in highp vec2 v_loc2;
layout(location = 0) out highp vec4 out_var_SV_Target0;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    highp vec4 _115;
    do
    {
        if (v_loc0.x >= 2.0)
        {
            highp vec2 _59 = v_loc0 - vec2(2.0);
            highp vec4 _63 = texture(u_fragment_tex1, _59);
            highp float _64 = _63.x;
            highp float _65 = _63.y;
            highp float _66 = isnan(_65) ? _64 : (isnan(_64) ? _65 : min(_64, _65));
            highp float _67 = isnan(_65) ? _64 : (isnan(_64) ? _65 : max(_64, _65));
            highp float _68 = _63.z;
            highp float _69 = isnan(_68) ? _67 : (isnan(_67) ? _68 : min(_67, _68));
            uvec2 _71 = uvec2(textureSize(u_fragment_tex1, 0));
            highp vec2 _81 = fwidth(_59);
            bvec2 _147 = isnan(_81);
            bvec2 _148 = isnan(vec2(9.9999999747524270787835121154785e-07));
            highp vec2 _149 = max(_81, vec2(9.9999999747524270787835121154785e-07));
            highp vec2 _150 = vec2(_147.x ? vec2(9.9999999747524270787835121154785e-07).x : _149.x, _147.y ? vec2(9.9999999747524270787835121154785e-07).y : _149.y);
            highp float _85 = 0.5 * dot(vec2(FragmentUniformBlock.DistanceRange) / vec2(float(_71.x), float(_71.y)), vec2(1.0) / vec2(_148.x ? _81.x : _150.x, _148.y ? _81.y : _150.y));
            _115 = v_loc1 * clamp((((isnan(1.0) ? _85 : (isnan(_85) ? 1.0 : max(_85, 1.0))) * ((isnan(_69) ? _66 : (isnan(_66) ? _69 : max(_66, _69))) - 0.5)) / (isnan(0.5) ? FragmentUniformBlock.TextAntialiasWidth : (isnan(FragmentUniformBlock.TextAntialiasWidth) ? 0.5 : max(FragmentUniformBlock.TextAntialiasWidth, 0.5)))) + 0.5, 0.0, 1.0);
            break;
        }
        _115 = (v_loc1 * texture(u_fragment_tex0, (vec4(v_loc2 / vec2(isnan(9.9999999747524270787835121154785e-07) ? FragmentUniformBlock.DpiScale : (isnan(FragmentUniformBlock.DpiScale) ? 9.9999999747524270787835121154785e-07 : max(FragmentUniformBlock.DpiScale, 9.9999999747524270787835121154785e-07))), 0.0, 1.0) * spvWorkaroundRowMajor(FragmentUniformBlock.BrushTextureMatrix)).xy)) * clamp(v_loc0.x, 0.0, 1.0);
        break;
    } while(false);
    out_var_SV_Target0 = _115;
}
