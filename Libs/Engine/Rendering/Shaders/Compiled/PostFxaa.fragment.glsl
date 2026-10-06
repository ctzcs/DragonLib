#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    highp vec4 Texel;
} FxaaSettings;

uniform highp sampler2D u_fragment_tex0;

in highp vec2 v_loc0;
layout(location = 0) out highp vec4 out_var_SV_Target0;

void main()
{
    highp vec4 _160;
    do
    {
        highp vec4 _59 = texture(u_fragment_tex0, v_loc0);
        highp vec4 _66 = texture(u_fragment_tex0, v_loc0 + (FxaaSettings.Texel.xy * vec2(-1.0)));
        highp float _68 = dot(_66.xyz, vec3(0.2989999949932098388671875, 0.58700001239776611328125, 0.114000000059604644775390625));
        highp vec4 _72 = texture(u_fragment_tex0, v_loc0 + (FxaaSettings.Texel.xy * vec2(1.0, -1.0)));
        highp float _74 = dot(_72.xyz, vec3(0.2989999949932098388671875, 0.58700001239776611328125, 0.114000000059604644775390625));
        highp vec4 _78 = texture(u_fragment_tex0, v_loc0 + (FxaaSettings.Texel.xy * vec2(-1.0, 1.0)));
        highp float _80 = dot(_78.xyz, vec3(0.2989999949932098388671875, 0.58700001239776611328125, 0.114000000059604644775390625));
        highp vec4 _83 = texture(u_fragment_tex0, v_loc0 + FxaaSettings.Texel.xy);
        highp float _85 = dot(_83.xyz, vec3(0.2989999949932098388671875, 0.58700001239776611328125, 0.114000000059604644775390625));
        highp float _87 = dot(_59.xyz, vec3(0.2989999949932098388671875, 0.58700001239776611328125, 0.114000000059604644775390625));
        highp float _88 = isnan(_74) ? _68 : (isnan(_68) ? _74 : min(_68, _74));
        highp float _89 = isnan(_85) ? _80 : (isnan(_80) ? _85 : min(_80, _85));
        highp float _90 = isnan(_89) ? _88 : (isnan(_88) ? _89 : min(_88, _89));
        highp float _91 = isnan(_90) ? _87 : (isnan(_87) ? _90 : min(_87, _90));
        highp float _92 = isnan(_74) ? _68 : (isnan(_68) ? _74 : max(_68, _74));
        highp float _93 = isnan(_85) ? _80 : (isnan(_80) ? _85 : max(_80, _85));
        highp float _94 = isnan(_93) ? _92 : (isnan(_92) ? _93 : max(_92, _93));
        highp float _95 = isnan(_94) ? _87 : (isnan(_87) ? _94 : max(_87, _94));
        highp float _97 = _95 * 0.125;
        if ((_95 - _91) < (isnan(_97) ? 0.031199999153614044189453125 : (isnan(0.031199999153614044189453125) ? _97 : max(0.031199999153614044189453125, _97))))
        {
            _160 = _59;
            break;
        }
        highp float _102 = _68 + _74;
        highp float _105 = -((_102 - _80) - _85);
        highp float _108 = ((_68 + _80) - _74) - _85;
        highp float _112 = ((_102 + _80) + _85) * 0.03125;
        highp float _114 = abs(_105);
        highp float _115 = abs(_108);
        highp vec2 _121 = clamp(vec2(_105, _108) / vec2((isnan(_115) ? _114 : (isnan(_114) ? _115 : min(_114, _115))) + (isnan(0.0078125) ? _112 : (isnan(_112) ? 0.0078125 : max(_112, 0.0078125)))), vec2(-8.0), vec2(8.0)) * FxaaSettings.Texel.xy;
        highp vec2 _122 = _121 * vec2(0.16666667163372039794921875);
        highp vec4 _125 = texture(u_fragment_tex0, v_loc0 - _122);
        highp vec4 _129 = texture(u_fragment_tex0, v_loc0 + _122);
        highp vec3 _132 = (_125.xyz + _129.xyz) * 0.5;
        highp vec2 _134 = _121 * 0.5;
        highp vec4 _137 = texture(u_fragment_tex0, v_loc0 - _134);
        highp vec4 _141 = texture(u_fragment_tex0, v_loc0 + _134);
        highp vec3 _145 = (_132 * 0.5) + ((_137.xyz + _141.xyz) * 0.25);
        highp float _146 = dot(_145, vec3(0.2989999949932098388671875, 0.58700001239776611328125, 0.114000000059604644775390625));
        bool _152;
        if (!(_146 < _91))
        {
            _152 = _146 > _95;
        }
        else
        {
            _152 = true;
        }
        bvec3 _153 = bvec3(_152);
        _160 = vec4(vec3(_153.x ? _132.x : _145.x, _153.y ? _132.y : _145.y, _153.z ? _132.z : _145.z), _59.w);
        break;
    } while(false);
    out_var_SV_Target0 = _160;
}
