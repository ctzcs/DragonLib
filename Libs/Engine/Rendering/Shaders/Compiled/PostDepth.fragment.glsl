#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    layout(row_major) highp mat4 InverseProjection;
    highp vec4 FogColor;
    highp vec4 Fog;
    highp vec4 Ao;
    highp vec4 Viewport;
} DepthSettings;

uniform highp sampler2D u_fragment_tex0;
uniform highp sampler2D u_fragment_tex1;

in highp vec2 v_loc0;
layout(location = 0) out highp vec4 out_var_SV_Target0;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    highp vec4 _329;
    do
    {
        highp vec4 _75 = texture(u_fragment_tex0, v_loc0);
        highp vec4 _79 = texture(u_fragment_tex1, v_loc0);
        highp float _80 = _79.x;
        highp vec4 _88 = vec4((v_loc0 * vec2(2.0, -2.0)) + vec2(-1.0, 1.0), _80, 1.0) * spvWorkaroundRowMajor(DepthSettings.InverseProjection);
        highp vec3 _92 = _88.xyz / vec3(_88.w);
        highp vec3 _93 = dFdx(_92);
        highp vec3 _94 = dFdy(_92);
        highp vec3 _95 = cross(_93, _94);
        highp float _96 = length(_95);
        highp vec3 _99 = _95 / vec3(isnan(9.9999999747524270787835121154785e-07) ? _96 : (isnan(_96) ? 9.9999999747524270787835121154785e-07 : max(_96, 9.9999999747524270787835121154785e-07)));
        highp vec3 _106;
        if (dot(_99, -_92) < 0.0)
        {
            _106 = -_99;
        }
        else
        {
            _106 = _99;
        }
        if (_80 >= 0.999998986721038818359375)
        {
            _329 = _75;
            break;
        }
        bool _112 = DepthSettings.Viewport.w > 0.5;
        highp vec3 _148;
        if (_112)
        {
            _148 = _75.xyz;
        }
        else
        {
            highp float _117 = _75.x;
            highp float _126;
            if (_117 <= 0.040449999272823333740234375)
            {
                _126 = _117 * 0.077399380505084991455078125;
            }
            else
            {
                _126 = pow((_117 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _127 = _75.y;
            highp float _136;
            if (_127 <= 0.040449999272823333740234375)
            {
                _136 = _127 * 0.077399380505084991455078125;
            }
            else
            {
                _136 = pow((_127 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _137 = _75.z;
            highp float _146;
            if (_137 <= 0.040449999272823333740234375)
            {
                _146 = _137 * 0.077399380505084991455078125;
            }
            else
            {
                _146 = pow((_137 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _148 = vec3(_126, _136, _146);
        }
        highp vec3 _240;
        if (DepthSettings.Ao.x > 0.5)
        {
            highp float _160 = -_92.z;
            highp float _167 = clamp(((DepthSettings.Ao.y * DepthSettings.Viewport.z) / (isnan(0.001000000047497451305389404296875) ? _160 : (isnan(_160) ? 0.001000000047497451305389404296875 : max(_160, 0.001000000047497451305389404296875)))) * 0.5, 1.0 / DepthSettings.Viewport.y, 0.1500000059604644775390625);
            highp float _169;
            _169 = 0.0;
            highp float _170;
            for (int _172 = 0; _172 < 16; _169 = _170, _172++)
            {
                highp float _177 = float(_172);
                highp float _178 = _177 * 2.399962902069091796875;
                highp vec2 _191 = v_loc0 + ((vec2((cos(_178) * DepthSettings.Viewport.y) / DepthSettings.Viewport.x, sin(_178)) * _167) * sqrt((_177 + 0.5) * 0.0625));
                bool _199;
                if (!any(lessThan(_191, vec2(0.0))))
                {
                    _199 = any(greaterThan(_191, vec2(1.0)));
                }
                else
                {
                    _199 = true;
                }
                if (_199)
                {
                    _170 = _169;
                    continue;
                }
                highp vec4 _203 = texture(u_fragment_tex1, _191);
                highp float _204 = _203.x;
                if (_204 >= 0.999998986721038818359375)
                {
                    _170 = _169;
                    continue;
                }
                highp vec4 _213 = vec4((_191 * vec2(2.0, -2.0)) + vec2(-1.0, 1.0), _204, 1.0) * spvWorkaroundRowMajor(DepthSettings.InverseProjection);
                highp vec3 _218 = (_213.xyz / vec3(_213.w)) - _92;
                highp float _219 = length(_218);
                _170 = _169 + (clamp((dot(_106, _218) - DepthSettings.Ao.w) / (isnan(0.001000000047497451305389404296875) ? _219 : (isnan(_219) ? 0.001000000047497451305389404296875 : max(_219, 0.001000000047497451305389404296875))), 0.0, 1.0) * clamp(1.0 - (_219 / (isnan(0.001000000047497451305389404296875) ? DepthSettings.Ao.y : (isnan(DepthSettings.Ao.y) ? 0.001000000047497451305389404296875 : max(DepthSettings.Ao.y, 0.001000000047497451305389404296875)))), 0.0, 1.0));
            }
            _240 = _148 * clamp(1.0 - ((_169 * DepthSettings.Ao.z) * 0.0625), 0.0, 1.0);
        }
        else
        {
            _240 = _148;
        }
        highp float _241 = length(_92);
        highp float _280;
        if (DepthSettings.Fog.x < 0.5)
        {
            _280 = 0.0;
        }
        else
        {
            highp float _279;
            if (DepthSettings.Fog.x < 1.5)
            {
                highp float _257 = DepthSettings.Fog.z - DepthSettings.Fog.y;
                _279 = clamp((_241 - DepthSettings.Fog.y) / (isnan(0.001000000047497451305389404296875) ? _257 : (isnan(_257) ? 0.001000000047497451305389404296875 : max(_257, 0.001000000047497451305389404296875))), 0.0, 1.0);
            }
            else
            {
                highp float _278;
                if (DepthSettings.Fog.x < 2.5)
                {
                    _278 = 1.0 - exp((-DepthSettings.Fog.w) * _241);
                }
                else
                {
                    _278 = 1.0 - exp(-pow(DepthSettings.Fog.w * _241, 2.0));
                }
                _279 = _278;
            }
            _280 = _279;
        }
        highp vec3 _285 = mix(_240, DepthSettings.FogColor.xyz, vec3(_280));
        highp vec3 _323;
        if (_112)
        {
            _323 = _285;
        }
        else
        {
            highp float _289 = _285.x;
            highp float _299;
            if (_289 <= 0.003130800090730190277099609375)
            {
                _299 = _289 * 12.9200000762939453125;
            }
            else
            {
                _299 = (1.05499994754791259765625 * pow(isnan(0.0) ? _289 : (isnan(_289) ? 0.0 : max(_289, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _300 = _285.y;
            highp float _310;
            if (_300 <= 0.003130800090730190277099609375)
            {
                _310 = _300 * 12.9200000762939453125;
            }
            else
            {
                _310 = (1.05499994754791259765625 * pow(isnan(0.0) ? _300 : (isnan(_300) ? 0.0 : max(_300, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _311 = _285.z;
            highp float _321;
            if (_311 <= 0.003130800090730190277099609375)
            {
                _321 = _311 * 12.9200000762939453125;
            }
            else
            {
                _321 = (1.05499994754791259765625 * pow(isnan(0.0) ? _311 : (isnan(_311) ? 0.0 : max(_311, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            _323 = vec3(_299, _310, _321);
        }
        _329 = vec4(_323, _75.w);
        break;
    } while(false);
    out_var_SV_Target0 = _329;
}
