#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    highp vec4 LightDirection;
    highp vec4 Ambient;
    highp vec4 Diffuse;
    highp vec4 CameraPosition;
    highp vec4 ColorPipeline;
} Standard3DLightBlock;

layout(std140) uniform FragmentUniform1
{
    highp vec4 BaseColorFactor;
    highp vec4 MaterialFlags;
    highp vec4 AlphaParams;
    highp vec4 PbrParams;
} Standard3DMaterialBlock;

layout(std140) uniform FragmentUniform2
{
    highp vec4 ShadowSettings;
} Standard3DShadowBlock;

layout(std140) uniform FragmentUniform3
{
    highp vec4 PointLightMeta;
    highp vec4 PointLightPositionRange[16];
    highp vec4 PointLightColorIntensity[16];
} Standard3DPointLightBlock;

uniform highp sampler2D u_fragment_tex0;
uniform highp sampler2D u_fragment_tex1;
uniform highp sampler2D u_fragment_tex2;

in highp vec3 v_loc0;
in highp vec4 v_loc1;
in highp vec2 v_loc2;
in highp vec4 v_loc3;
in highp vec3 v_loc4;
layout(location = 0) out highp vec4 out_var_SV_Target0;

void main()
{
    highp vec4 _192;
    if (Standard3DMaterialBlock.MaterialFlags.x > 0.5)
    {
        highp vec4 _104 = texture(u_fragment_tex0, v_loc2);
        bool _113;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _113 = Standard3DMaterialBlock.PbrParams.z < 0.5;
        }
        else
        {
            _113 = false;
        }
        highp vec4 _148;
        if (_113)
        {
            highp float _116 = _104.x;
            highp float _125;
            if (_116 <= 0.040449999272823333740234375)
            {
                _125 = _116 * 0.077399380505084991455078125;
            }
            else
            {
                _125 = pow((_116 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _126 = _104.y;
            highp float _135;
            if (_126 <= 0.040449999272823333740234375)
            {
                _135 = _126 * 0.077399380505084991455078125;
            }
            else
            {
                _135 = pow((_126 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _136 = _104.z;
            highp float _145;
            if (_136 <= 0.040449999272823333740234375)
            {
                _145 = _136 * 0.077399380505084991455078125;
            }
            else
            {
                _145 = pow((_136 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp vec3 _146 = vec3(_125, _135, _145);
            _148 = vec4(_146.x, _146.y, _146.z, _104.w);
        }
        else
        {
            _148 = _104;
        }
        bool _155;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _155 = Standard3DMaterialBlock.PbrParams.z > 0.5;
        }
        else
        {
            _155 = false;
        }
        highp vec4 _190;
        if (_155)
        {
            highp float _167;
            if (_148.x <= 0.003130800090730190277099609375)
            {
                _167 = _148.x * 12.9200000762939453125;
            }
            else
            {
                _167 = (1.05499994754791259765625 * pow(_148.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _177;
            if (_148.y <= 0.003130800090730190277099609375)
            {
                _177 = _148.y * 12.9200000762939453125;
            }
            else
            {
                _177 = (1.05499994754791259765625 * pow(_148.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _187;
            if (_148.z <= 0.003130800090730190277099609375)
            {
                _187 = _148.z * 12.9200000762939453125;
            }
            else
            {
                _187 = (1.05499994754791259765625 * pow(_148.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp vec3 _188 = vec3(_167, _177, _187);
            _190 = vec4(_188.x, _188.y, _188.z, _148.w);
        }
        else
        {
            _190 = _148;
        }
        _192 = Standard3DMaterialBlock.BaseColorFactor * _190;
    }
    else
    {
        _192 = Standard3DMaterialBlock.BaseColorFactor;
    }
    bool _199;
    if (Standard3DMaterialBlock.MaterialFlags.w > 0.5)
    {
        _199 = Standard3DMaterialBlock.MaterialFlags.w < 1.5;
    }
    else
    {
        _199 = false;
    }
    if (_199)
    {
        if ((_192.w - Standard3DMaterialBlock.AlphaParams.x) < 0.0)
        {
            discard;
        }
    }
    highp vec3 _209 = normalize(v_loc0);
    highp vec3 _236;
    if (Standard3DMaterialBlock.MaterialFlags.y > 0.5)
    {
        highp vec3 _216 = normalize(v_loc1.xyz);
        highp vec3 _228 = (texture(u_fragment_tex1, v_loc2).xyz * 2.0) - vec3(1.0);
        highp vec2 _232 = _228.xy * Standard3DMaterialBlock.MaterialFlags.z;
        _236 = normalize(mat3(_216, cross(_209, _216) * v_loc1.w, _209) * vec3(_232.x, _232.y, _228.z));
    }
    else
    {
        _236 = _209;
    }
    highp float _240 = clamp(Standard3DMaterialBlock.PbrParams.x, 0.0, 1.0);
    highp float _243 = clamp(Standard3DMaterialBlock.PbrParams.y, 0.0500000007450580596923828125, 1.0);
    highp vec3 _248 = normalize(Standard3DLightBlock.CameraPosition.xyz - v_loc4);
    highp float _325;
    do
    {
        if (Standard3DShadowBlock.ShadowSettings.x < 0.5)
        {
            _325 = 1.0;
            break;
        }
        highp vec3 _260 = v_loc3.xyz / vec3(v_loc3.w);
        highp vec2 _263 = (_260.xy * 0.5) + vec2(0.5);
        highp float _264 = _263.x;
        bool _270;
        if (!(_264 < 0.0))
        {
            _270 = _264 > 1.0;
        }
        else
        {
            _270 = true;
        }
        bool _276;
        if (!_270)
        {
            _276 = _263.y < 0.0;
        }
        else
        {
            _276 = true;
        }
        bool _282;
        if (!_276)
        {
            _282 = _263.y > 1.0;
        }
        else
        {
            _282 = true;
        }
        if (_282)
        {
            _325 = 1.0;
            break;
        }
        highp float _292;
        int _295;
        _292 = 0.0;
        _295 = -1;
        highp float _293;
        for (; _295 <= 1; _292 = _293, _295++)
        {
            _293 = _292;
            for (int _303 = -1; _303 <= 1; )
            {
                _293 += float((_260.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, _263 + (vec2(float(_303), float(_295)) * Standard3DShadowBlock.ShadowSettings.y)).x);
                _303++;
                continue;
            }
        }
        _325 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_292 * 0.111111111938953399658203125)));
        break;
    } while(false);
    highp vec3 _330 = normalize(-Standard3DLightBlock.LightDirection.xyz);
    highp vec3 _336 = normalize(_248 + _330);
    highp float _338 = clamp(dot(_236, _330), 0.0, 1.0);
    highp float _340 = clamp(dot(_236, _248), 0.0, 1.0);
    highp vec3 _342 = mix(vec3(0.039999999105930328369140625), _192.xyz, vec3(_240));
    highp vec3 _345 = vec3(1.0) - _342;
    highp vec3 _349 = _342 + (_345 * pow(1.0 - clamp(dot(_336, _248), 0.0, 1.0), 5.0));
    highp float _350 = _243 * _243;
    highp float _351 = _350 * _350;
    highp float _353 = clamp(dot(_236, _336), 0.0, 1.0);
    highp float _355 = _351 - 1.0;
    highp float _357 = ((_353 * _353) * _355) + 1.0;
    highp float _361 = _243 + 1.0;
    highp float _363 = (_361 * _361) * 0.125;
    highp float _364 = 1.0 - _363;
    highp float _367 = _340 / ((_340 * _364) + _363);
    highp float _374 = 4.0 * _340;
    highp float _375 = _374 * _338;
    highp float _380 = 1.0 - _240;
    int _389 = min(int(Standard3DPointLightBlock.PointLightMeta.x), 16);
    highp vec3 _391;
    _391 = (((((vec3(1.0) - _349) * _380) * _192.xyz) + ((_349 * ((_351 / ((3.1415927410125732421875 * _357) * _357)) * (_367 * (_338 / ((_338 * _364) + _363))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _375 : (isnan(_375) ? 9.9999997473787516355514526367188e-05 : max(_375, 9.9999997473787516355514526367188e-05))))) * (Standard3DLightBlock.Diffuse.xyz * _325)) * _338;
    for (int _394 = 0; _394 < _389; )
    {
        highp vec3 _401 = Standard3DPointLightBlock.PointLightPositionRange[_394].xyz - v_loc4;
        highp float _402 = length(_401);
        highp float _408 = clamp(1.0 - (_402 / (isnan(0.001000000047497451305389404296875) ? Standard3DPointLightBlock.PointLightPositionRange[_394].w : (isnan(Standard3DPointLightBlock.PointLightPositionRange[_394].w) ? 0.001000000047497451305389404296875 : max(Standard3DPointLightBlock.PointLightPositionRange[_394].w, 0.001000000047497451305389404296875)))), 0.0, 1.0);
        highp vec3 _419 = _401 / vec3(isnan(9.9999997473787516355514526367188e-05) ? _402 : (isnan(_402) ? 9.9999997473787516355514526367188e-05 : max(_402, 9.9999997473787516355514526367188e-05)));
        highp vec3 _421 = normalize(_248 + _419);
        highp float _423 = clamp(dot(_236, _419), 0.0, 1.0);
        highp vec3 _429 = _342 + (_345 * pow(1.0 - clamp(dot(_421, _248), 0.0, 1.0), 5.0));
        highp float _431 = clamp(dot(_236, _421), 0.0, 1.0);
        highp float _434 = ((_431 * _431) * _355) + 1.0;
        highp float _444 = _374 * _423;
        _391 += ((((((vec3(1.0) - _429) * _380) * _192.xyz) + ((_429 * ((_351 / ((3.1415927410125732421875 * _434) * _434)) * (_367 * (_423 / ((_423 * _364) + _363))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _444 : (isnan(_444) ? 9.9999997473787516355514526367188e-05 : max(_444, 9.9999997473787516355514526367188e-05))))) * ((Standard3DPointLightBlock.PointLightColorIntensity[_394].xyz * Standard3DPointLightBlock.PointLightColorIntensity[_394].w) * (_408 * _408))) * _423);
        _394++;
        continue;
    }
    highp vec3 _458 = _391 + (Standard3DLightBlock.Ambient.xyz * _192.xyz);
    highp vec3 _466;
    if (Standard3DLightBlock.ColorPipeline.x > 0.5)
    {
        _466 = _458;
    }
    else
    {
        _466 = clamp(_458, vec3(0.0), vec3(1.0));
    }
    out_var_SV_Target0 = vec4(_466, _192.w);
}
