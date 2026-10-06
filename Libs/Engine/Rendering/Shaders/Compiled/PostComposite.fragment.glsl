#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    highp vec4 Settings;
} CompositeSettings;

uniform highp sampler2D u_fragment_tex0;
uniform highp sampler2D u_fragment_tex1;

in highp vec2 v_loc0;
layout(location = 0) out highp vec4 out_var_SV_Target0;

void main()
{
    highp vec4 _132;
    do
    {
        highp vec4 _59 = texture(u_fragment_tex0, v_loc0);
        if (CompositeSettings.Settings.z < 0.5)
        {
            _132 = _59;
            break;
        }
        highp vec4 _69 = texture(u_fragment_tex1, v_loc0);
        highp vec3 _77 = (_59.xyz + (_69.xyz * CompositeSettings.Settings.w)) * CompositeSettings.Settings.x;
        bvec3 _138 = isnan(_77);
        bvec3 _139 = isnan(vec3(0.0));
        highp vec3 _140 = max(_77, vec3(0.0));
        highp vec3 _141 = vec3(_138.x ? vec3(0.0).x : _140.x, _138.y ? vec3(0.0).y : _140.y, _138.z ? vec3(0.0).z : _140.z);
        highp vec3 _78 = vec3(_139.x ? _77.x : _141.x, _139.y ? _77.y : _141.y, _139.z ? _77.z : _141.z);
        highp vec3 _96;
        if (CompositeSettings.Settings.y > 0.5)
        {
            _96 = _78 / (vec3(1.0) + _78);
        }
        else
        {
            _96 = clamp((_78 * ((_78 * 2.5099999904632568359375) + vec3(0.02999999932944774627685546875))) / ((_78 * ((_78 * 2.4300000667572021484375) + vec3(0.589999973773956298828125))) + vec3(0.14000000059604644775390625)), vec3(0.0), vec3(1.0));
        }
        highp float _107;
        if (_96.x <= 0.003130800090730190277099609375)
        {
            _107 = _96.x * 12.9200000762939453125;
        }
        else
        {
            _107 = (1.05499994754791259765625 * pow(isnan(0.0) ? _96.x : (isnan(_96.x) ? 0.0 : max(_96.x, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        highp float _118;
        if (_96.y <= 0.003130800090730190277099609375)
        {
            _118 = _96.y * 12.9200000762939453125;
        }
        else
        {
            _118 = (1.05499994754791259765625 * pow(isnan(0.0) ? _96.y : (isnan(_96.y) ? 0.0 : max(_96.y, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        highp float _129;
        if (_96.z <= 0.003130800090730190277099609375)
        {
            _129 = _96.z * 12.9200000762939453125;
        }
        else
        {
            _129 = (1.05499994754791259765625 * pow(isnan(0.0) ? _96.z : (isnan(_96.z) ? 0.0 : max(_96.z, 0.0)), 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        _132 = vec4(_107, _118, _129, _59.w);
        break;
    } while(false);
    out_var_SV_Target0 = _132;
}
