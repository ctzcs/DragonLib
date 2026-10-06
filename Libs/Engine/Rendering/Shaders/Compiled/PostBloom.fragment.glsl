#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    highp vec4 Blur;
    highp vec4 Extract;
} BloomSettings;

uniform highp sampler2D u_fragment_tex0;

in highp vec2 v_loc0;
layout(location = 0) out highp vec4 out_var_SV_Target0;

void main()
{
    highp vec4 _163;
    do
    {
        if (BloomSettings.Blur.w > 0.5)
        {
            highp vec3 _61;
            int _64;
            _61 = vec3(0.0);
            _64 = 0;
            highp vec3 _62;
            for (; _64 < 4; _61 = _62, _64++)
            {
                _62 = _61;
                for (int _72 = 0; _72 < 4; )
                {
                    highp vec4 _88 = texture(u_fragment_tex0, v_loc0 + ((vec2(float(_72), float(_64)) - vec2(1.5)) * BloomSettings.Extract.xy));
                    highp float _90 = _88.x;
                    highp float _91 = _88.y;
                    highp float _92 = _88.z;
                    highp float _93 = isnan(_92) ? _91 : (isnan(_91) ? _92 : max(_91, _92));
                    highp float _94 = isnan(_93) ? _90 : (isnan(_90) ? _93 : max(_90, _93));
                    highp float _99 = BloomSettings.Blur.z * BloomSettings.Extract.z;
                    highp float _100 = isnan(9.9999997473787516355514526367188e-06) ? _99 : (isnan(_99) ? 9.9999997473787516355514526367188e-06 : max(_99, 9.9999997473787516355514526367188e-06));
                    highp float _101 = _94 - BloomSettings.Blur.z;
                    highp float _104 = clamp(_101 + _100, 0.0, 2.0 * _100);
                    highp float _107 = (_104 * _104) / (4.0 * _100);
                    _62 += ((_88.xyz * (isnan(_107) ? _101 : (isnan(_101) ? _107 : max(_101, _107)))) / vec3(isnan(9.9999997473787516355514526367188e-06) ? _94 : (isnan(_94) ? 9.9999997473787516355514526367188e-06 : max(_94, 9.9999997473787516355514526367188e-06))));
                    _72++;
                    continue;
                }
            }
            _163 = vec4(_61 * vec3(0.0625), 1.0);
            break;
        }
        highp vec3 _125;
        _125 = texture(u_fragment_tex0, v_loc0).xyz * 0.227026998996734619140625;
        highp vec3 _126;
        for (int _128 = 1; _128 <= 4; _125 = _126, _128++)
        {
            highp float _144;
            if (_128 == 1)
            {
                _144 = 0.19459499418735504150390625;
            }
            else
            {
                highp float _143;
                if (_128 == 2)
                {
                    _143 = 0.1216219961643218994140625;
                }
                else
                {
                    _143 = (_128 == 3) ? 0.054053999483585357666015625 : 0.0162159986793994903564453125;
                }
                _144 = _143;
            }
            highp vec2 _148 = BloomSettings.Blur.xy * float(_128);
            _126 = _125 + ((texture(u_fragment_tex0, v_loc0 + _148).xyz + texture(u_fragment_tex0, v_loc0 - _148).xyz) * _144);
        }
        _163 = vec4(_125, 1.0);
        break;
    } while(false);
    out_var_SV_Target0 = _163;
}
