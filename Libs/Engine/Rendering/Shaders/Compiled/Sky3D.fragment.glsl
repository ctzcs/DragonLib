#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    layout(row_major) highp mat4 InverseViewProjection;
    highp vec4 CameraPosition;
    highp vec4 Settings;
} SkySettings;

uniform highp sampler2D u_fragment_tex0;

in highp vec2 v_loc0;
layout(location = 0) out highp vec4 out_var_SV_Target0;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    highp vec4 _56 = vec4((v_loc0 * vec2(2.0, -2.0)) + vec2(-1.0, 1.0), 1.0, 1.0) * spvWorkaroundRowMajor(SkySettings.InverseViewProjection);
    highp vec3 _65 = normalize((_56.xyz / vec3(_56.w)) - SkySettings.CameraPosition.xyz);
    highp float _70 = sin(SkySettings.Settings.y);
    highp float _71 = cos(SkySettings.Settings.y);
    highp float _72 = _65.x;
    highp float _74 = _65.z;
    highp vec3 _82 = normalize(vec3((_71 * _72) - (_70 * _74), _65.y, (_70 * _72) + (_71 * _74)));
    highp vec4 _94 = texture(u_fragment_tex0, vec2((atan(_82.z, _82.x) * 0.15915493667125701904296875) + 0.5, acos(clamp(_82.y, -1.0, 1.0)) * 0.3183098733425140380859375));
    highp vec3 _98 = _94.xyz * SkySettings.Settings.x;
    highp vec3 _139;
    if (SkySettings.Settings.z > 0.5)
    {
        _139 = _98;
    }
    else
    {
        highp float _105 = _98.x;
        highp float _115;
        if (_105 <= 0.003130800090730190277099609375)
        {
            _115 = _105 * 12.9200000762939453125;
        }
        else
        {
            _115 = (1.05499994754791259765625 * pow(isnan(0.0) ? _105 : (isnan(_105) ? 0.0 : max(_105, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        highp float _116 = _98.y;
        highp float _126;
        if (_116 <= 0.003130800090730190277099609375)
        {
            _126 = _116 * 12.9200000762939453125;
        }
        else
        {
            _126 = (1.05499994754791259765625 * pow(isnan(0.0) ? _116 : (isnan(_116) ? 0.0 : max(_116, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        highp float _127 = _98.z;
        highp float _137;
        if (_127 <= 0.003130800090730190277099609375)
        {
            _137 = _127 * 12.9200000762939453125;
        }
        else
        {
            _137 = (1.05499994754791259765625 * pow(isnan(0.0) ? _127 : (isnan(_127) ? 0.0 : max(_127, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        _139 = vec3(_115, _126, _137);
    }
    out_var_SV_Target0 = vec4(_139, 1.0);
}
