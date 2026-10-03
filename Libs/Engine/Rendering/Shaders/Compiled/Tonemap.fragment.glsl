#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    highp vec4 Settings;
} TonemapBlock;

uniform highp sampler2D u_fragment_tex0;

in highp vec2 v_loc0;
layout(location = 0) out highp vec4 out_var_SV_Target0;

void main()
{
    highp vec4 _52 = texture(u_fragment_tex0, v_loc0);
    highp vec3 _56 = _52.xyz * TonemapBlock.Settings.x;
    bvec3 _121 = isnan(_56);
    bvec3 _122 = isnan(vec3(0.0));
    highp vec3 _123 = max(_56, vec3(0.0));
    highp vec3 _124 = vec3(_121.x ? vec3(0.0).x : _123.x, _121.y ? vec3(0.0).y : _123.y, _121.z ? vec3(0.0).z : _123.z);
    highp vec3 _57 = vec3(_122.x ? _56.x : _124.x, _122.y ? _56.y : _124.y, _122.z ? _56.z : _124.z);
    highp vec3 _75;
    if (TonemapBlock.Settings.y > 0.5)
    {
        _75 = _57 / (vec3(1.0) + _57);
    }
    else
    {
        _75 = clamp((_57 * ((_57 * 2.5099999904632568359375) + vec3(0.02999999932944774627685546875))) / ((_57 * ((_57 * 2.4300000667572021484375) + vec3(0.589999973773956298828125))) + vec3(0.14000000059604644775390625)), vec3(0.0), vec3(1.0));
    }
    highp vec3 _112;
    if (TonemapBlock.Settings.z > 0.5)
    {
        highp float _90;
        if (_75.x <= 0.003130800090730190277099609375)
        {
            _90 = _75.x * 12.9200000762939453125;
        }
        else
        {
            _90 = (1.05499994754791259765625 * pow(_75.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        highp float _100;
        if (_75.y <= 0.003130800090730190277099609375)
        {
            _100 = _75.y * 12.9200000762939453125;
        }
        else
        {
            _100 = (1.05499994754791259765625 * pow(_75.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        highp float _110;
        if (_75.z <= 0.003130800090730190277099609375)
        {
            _110 = _75.z * 12.9200000762939453125;
        }
        else
        {
            _110 = (1.05499994754791259765625 * pow(_75.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
        }
        _112 = vec3(_90, _100, _110);
    }
    else
    {
        _112 = _75;
    }
    out_var_SV_Target0 = vec4(_112, _52.w);
}
