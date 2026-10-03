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
    highp vec4 TextureFlags;
    highp vec4 Emissive;
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
uniform highp sampler2D u_fragment_tex3;
uniform highp sampler2D u_fragment_tex2;
uniform highp sampler2D u_fragment_tex4;
uniform highp sampler2D u_fragment_tex5;

in highp vec3 v_loc0;
in highp vec4 v_loc1;
in highp vec2 v_loc2;
in highp vec4 v_loc3;
in highp vec3 v_loc4;
layout(location = 0) out highp vec4 out_var_SV_Target0;

void main()
{
    highp vec4 _199;
    if (Standard3DMaterialBlock.MaterialFlags.x > 0.5)
    {
        highp vec4 _111 = texture(u_fragment_tex0, v_loc2);
        bool _120;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _120 = Standard3DMaterialBlock.PbrParams.z < 0.5;
        }
        else
        {
            _120 = false;
        }
        highp vec4 _155;
        if (_120)
        {
            highp float _123 = _111.x;
            highp float _132;
            if (_123 <= 0.040449999272823333740234375)
            {
                _132 = _123 * 0.077399380505084991455078125;
            }
            else
            {
                _132 = pow((_123 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _133 = _111.y;
            highp float _142;
            if (_133 <= 0.040449999272823333740234375)
            {
                _142 = _133 * 0.077399380505084991455078125;
            }
            else
            {
                _142 = pow((_133 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _143 = _111.z;
            highp float _152;
            if (_143 <= 0.040449999272823333740234375)
            {
                _152 = _143 * 0.077399380505084991455078125;
            }
            else
            {
                _152 = pow((_143 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp vec3 _153 = vec3(_132, _142, _152);
            _155 = vec4(_153.x, _153.y, _153.z, _111.w);
        }
        else
        {
            _155 = _111;
        }
        bool _162;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _162 = Standard3DMaterialBlock.PbrParams.z > 0.5;
        }
        else
        {
            _162 = false;
        }
        highp vec4 _197;
        if (_162)
        {
            highp float _174;
            if (_155.x <= 0.003130800090730190277099609375)
            {
                _174 = _155.x * 12.9200000762939453125;
            }
            else
            {
                _174 = (1.05499994754791259765625 * pow(_155.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _184;
            if (_155.y <= 0.003130800090730190277099609375)
            {
                _184 = _155.y * 12.9200000762939453125;
            }
            else
            {
                _184 = (1.05499994754791259765625 * pow(_155.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _194;
            if (_155.z <= 0.003130800090730190277099609375)
            {
                _194 = _155.z * 12.9200000762939453125;
            }
            else
            {
                _194 = (1.05499994754791259765625 * pow(_155.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp vec3 _195 = vec3(_174, _184, _194);
            _197 = vec4(_195.x, _195.y, _195.z, _155.w);
        }
        else
        {
            _197 = _155;
        }
        _199 = Standard3DMaterialBlock.BaseColorFactor * _197;
    }
    else
    {
        _199 = Standard3DMaterialBlock.BaseColorFactor;
    }
    bool _206;
    if (Standard3DMaterialBlock.MaterialFlags.w > 0.5)
    {
        _206 = Standard3DMaterialBlock.MaterialFlags.w < 1.5;
    }
    else
    {
        _206 = false;
    }
    if (_206)
    {
        if ((_199.w - Standard3DMaterialBlock.AlphaParams.x) < 0.0)
        {
            discard;
        }
    }
    highp vec3 _216 = normalize(v_loc0);
    highp vec3 _243;
    if (Standard3DMaterialBlock.MaterialFlags.y > 0.5)
    {
        highp vec3 _223 = normalize(v_loc1.xyz);
        highp vec3 _235 = (texture(u_fragment_tex1, v_loc2).xyz * 2.0) - vec3(1.0);
        highp vec2 _239 = _235.xy * Standard3DMaterialBlock.MaterialFlags.z;
        _243 = normalize(mat3(_223, cross(_216, _223) * v_loc1.w, _216) * vec3(_239.x, _239.y, _235.z));
    }
    else
    {
        _243 = _216;
    }
    highp vec2 _259;
    if (Standard3DMaterialBlock.TextureFlags.x > 0.5)
    {
        _259 = Standard3DMaterialBlock.PbrParams.xy * texture(u_fragment_tex3, v_loc2).zy;
    }
    else
    {
        _259 = Standard3DMaterialBlock.PbrParams.xy;
    }
    highp float _261 = clamp(_259.x, 0.0, 1.0);
    highp float _263 = clamp(_259.y, 0.0500000007450580596923828125, 1.0);
    highp vec3 _268 = normalize(Standard3DLightBlock.CameraPosition.xyz - v_loc4);
    highp float _345;
    do
    {
        if (Standard3DShadowBlock.ShadowSettings.x < 0.5)
        {
            _345 = 1.0;
            break;
        }
        highp vec3 _280 = v_loc3.xyz / vec3(v_loc3.w);
        highp vec2 _283 = (_280.xy * 0.5) + vec2(0.5);
        highp float _284 = _283.x;
        bool _290;
        if (!(_284 < 0.0))
        {
            _290 = _284 > 1.0;
        }
        else
        {
            _290 = true;
        }
        bool _296;
        if (!_290)
        {
            _296 = _283.y < 0.0;
        }
        else
        {
            _296 = true;
        }
        bool _302;
        if (!_296)
        {
            _302 = _283.y > 1.0;
        }
        else
        {
            _302 = true;
        }
        if (_302)
        {
            _345 = 1.0;
            break;
        }
        highp float _312;
        int _315;
        _312 = 0.0;
        _315 = -1;
        highp float _313;
        for (; _315 <= 1; _312 = _313, _315++)
        {
            _313 = _312;
            for (int _323 = -1; _323 <= 1; )
            {
                _313 += float((_280.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, _283 + (vec2(float(_323), float(_315)) * Standard3DShadowBlock.ShadowSettings.y)).x);
                _323++;
                continue;
            }
        }
        _345 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_312 * 0.111111111938953399658203125)));
        break;
    } while(false);
    highp vec3 _350 = normalize(-Standard3DLightBlock.LightDirection.xyz);
    highp vec3 _356 = normalize(_268 + _350);
    highp float _358 = clamp(dot(_243, _350), 0.0, 1.0);
    highp float _360 = clamp(dot(_243, _268), 0.0, 1.0);
    highp vec3 _362 = mix(vec3(0.039999999105930328369140625), _199.xyz, vec3(_261));
    highp vec3 _365 = vec3(1.0) - _362;
    highp vec3 _369 = _362 + (_365 * pow(1.0 - clamp(dot(_356, _268), 0.0, 1.0), 5.0));
    highp float _370 = _263 * _263;
    highp float _371 = _370 * _370;
    highp float _373 = clamp(dot(_243, _356), 0.0, 1.0);
    highp float _375 = _371 - 1.0;
    highp float _377 = ((_373 * _373) * _375) + 1.0;
    highp float _381 = _263 + 1.0;
    highp float _383 = (_381 * _381) * 0.125;
    highp float _384 = 1.0 - _383;
    highp float _387 = _360 / ((_360 * _384) + _383);
    highp float _394 = 4.0 * _360;
    highp float _395 = _394 * _358;
    highp float _400 = 1.0 - _261;
    int _409 = min(int(Standard3DPointLightBlock.PointLightMeta.x), 16);
    highp vec3 _411;
    _411 = (((((vec3(1.0) - _369) * _400) * _199.xyz) + ((_369 * ((_371 / ((3.1415927410125732421875 * _377) * _377)) * (_387 * (_358 / ((_358 * _384) + _383))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _395 : (isnan(_395) ? 9.9999997473787516355514526367188e-05 : max(_395, 9.9999997473787516355514526367188e-05))))) * (Standard3DLightBlock.Diffuse.xyz * _345)) * _358;
    for (int _414 = 0; _414 < _409; )
    {
        highp vec3 _421 = Standard3DPointLightBlock.PointLightPositionRange[_414].xyz - v_loc4;
        highp float _422 = length(_421);
        highp float _428 = clamp(1.0 - (_422 / (isnan(0.001000000047497451305389404296875) ? Standard3DPointLightBlock.PointLightPositionRange[_414].w : (isnan(Standard3DPointLightBlock.PointLightPositionRange[_414].w) ? 0.001000000047497451305389404296875 : max(Standard3DPointLightBlock.PointLightPositionRange[_414].w, 0.001000000047497451305389404296875)))), 0.0, 1.0);
        highp vec3 _439 = _421 / vec3(isnan(9.9999997473787516355514526367188e-05) ? _422 : (isnan(_422) ? 9.9999997473787516355514526367188e-05 : max(_422, 9.9999997473787516355514526367188e-05)));
        highp vec3 _441 = normalize(_268 + _439);
        highp float _443 = clamp(dot(_243, _439), 0.0, 1.0);
        highp vec3 _449 = _362 + (_365 * pow(1.0 - clamp(dot(_441, _268), 0.0, 1.0), 5.0));
        highp float _451 = clamp(dot(_243, _441), 0.0, 1.0);
        highp float _454 = ((_451 * _451) * _375) + 1.0;
        highp float _464 = _394 * _443;
        _411 += ((((((vec3(1.0) - _449) * _400) * _199.xyz) + ((_449 * ((_371 / ((3.1415927410125732421875 * _454) * _454)) * (_387 * (_443 / ((_443 * _384) + _383))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _464 : (isnan(_464) ? 9.9999997473787516355514526367188e-05 : max(_464, 9.9999997473787516355514526367188e-05))))) * ((Standard3DPointLightBlock.PointLightColorIntensity[_414].xyz * Standard3DPointLightBlock.PointLightColorIntensity[_414].w) * (_428 * _428))) * _443);
        _414++;
        continue;
    }
    highp float _488;
    if (Standard3DMaterialBlock.TextureFlags.y > 0.5)
    {
        _488 = mix(1.0, texture(u_fragment_tex4, v_loc2).x, Standard3DMaterialBlock.Emissive.w);
    }
    else
    {
        _488 = 1.0;
    }
    highp vec3 _593;
    if (Standard3DMaterialBlock.TextureFlags.z > 0.5)
    {
        highp vec4 _506 = texture(u_fragment_tex5, v_loc2);
        bool _516;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _516 = Standard3DMaterialBlock.TextureFlags.w < 0.5;
        }
        else
        {
            _516 = false;
        }
        highp vec3 _550;
        if (_516)
        {
            highp float _519 = _506.x;
            highp float _528;
            if (_519 <= 0.040449999272823333740234375)
            {
                _528 = _519 * 0.077399380505084991455078125;
            }
            else
            {
                _528 = pow((_519 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _529 = _506.y;
            highp float _538;
            if (_529 <= 0.040449999272823333740234375)
            {
                _538 = _529 * 0.077399380505084991455078125;
            }
            else
            {
                _538 = pow((_529 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _539 = _506.z;
            highp float _548;
            if (_539 <= 0.040449999272823333740234375)
            {
                _548 = _539 * 0.077399380505084991455078125;
            }
            else
            {
                _548 = pow((_539 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _550 = vec3(_528, _538, _548);
        }
        else
        {
            _550 = _506.xyz;
        }
        bool _557;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _557 = Standard3DMaterialBlock.TextureFlags.w > 0.5;
        }
        else
        {
            _557 = false;
        }
        highp vec3 _591;
        if (_557)
        {
            highp float _569;
            if (_550.x <= 0.003130800090730190277099609375)
            {
                _569 = _550.x * 12.9200000762939453125;
            }
            else
            {
                _569 = (1.05499994754791259765625 * pow(_550.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _579;
            if (_550.y <= 0.003130800090730190277099609375)
            {
                _579 = _550.y * 12.9200000762939453125;
            }
            else
            {
                _579 = (1.05499994754791259765625 * pow(_550.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _589;
            if (_550.z <= 0.003130800090730190277099609375)
            {
                _589 = _550.z * 12.9200000762939453125;
            }
            else
            {
                _589 = (1.05499994754791259765625 * pow(_550.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            _591 = vec3(_569, _579, _589);
        }
        else
        {
            _591 = _550;
        }
        _593 = Standard3DMaterialBlock.Emissive.xyz * _591;
    }
    else
    {
        _593 = Standard3DMaterialBlock.Emissive.xyz;
    }
    highp vec3 _594 = (_411 + ((Standard3DLightBlock.Ambient.xyz * _199.xyz) * _488)) + _593;
    highp vec3 _602;
    if (Standard3DLightBlock.ColorPipeline.x > 0.5)
    {
        _602 = _594;
    }
    else
    {
        _602 = clamp(_594, vec3(0.0), vec3(1.0));
    }
    out_var_SV_Target0 = vec4(_602, _199.w);
}
