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
    highp vec4 Environment;
    highp vec4 EnvironmentSize;
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
    layout(row_major) highp mat4 CascadeMatrices[4];
    highp vec4 CascadeSplits;
    highp vec4 CascadeSettings;
    highp vec4 ShadowCameraForward;
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
uniform highp sampler2D u_fragment_tex7;
uniform highp sampler2D u_fragment_tex8;
uniform highp sampler2D u_fragment_tex6;
uniform highp sampler2D u_fragment_tex5;

in highp vec3 v_loc0;
in highp vec4 v_loc1;
in highp vec2 v_loc2;
in highp vec4 v_loc3;
in highp vec3 v_loc4;
layout(location = 0) out highp vec4 out_var_SV_Target0;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    highp vec4 _224;
    if (Standard3DMaterialBlock.MaterialFlags.x > 0.5)
    {
        highp vec4 _136 = texture(u_fragment_tex0, v_loc2);
        bool _145;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _145 = Standard3DMaterialBlock.PbrParams.z < 0.5;
        }
        else
        {
            _145 = false;
        }
        highp vec4 _180;
        if (_145)
        {
            highp float _148 = _136.x;
            highp float _157;
            if (_148 <= 0.040449999272823333740234375)
            {
                _157 = _148 * 0.077399380505084991455078125;
            }
            else
            {
                _157 = pow((_148 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _158 = _136.y;
            highp float _167;
            if (_158 <= 0.040449999272823333740234375)
            {
                _167 = _158 * 0.077399380505084991455078125;
            }
            else
            {
                _167 = pow((_158 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _168 = _136.z;
            highp float _177;
            if (_168 <= 0.040449999272823333740234375)
            {
                _177 = _168 * 0.077399380505084991455078125;
            }
            else
            {
                _177 = pow((_168 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp vec3 _178 = vec3(_157, _167, _177);
            _180 = vec4(_178.x, _178.y, _178.z, _136.w);
        }
        else
        {
            _180 = _136;
        }
        bool _187;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _187 = Standard3DMaterialBlock.PbrParams.z > 0.5;
        }
        else
        {
            _187 = false;
        }
        highp vec4 _222;
        if (_187)
        {
            highp float _199;
            if (_180.x <= 0.003130800090730190277099609375)
            {
                _199 = _180.x * 12.9200000762939453125;
            }
            else
            {
                _199 = (1.05499994754791259765625 * pow(_180.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _209;
            if (_180.y <= 0.003130800090730190277099609375)
            {
                _209 = _180.y * 12.9200000762939453125;
            }
            else
            {
                _209 = (1.05499994754791259765625 * pow(_180.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _219;
            if (_180.z <= 0.003130800090730190277099609375)
            {
                _219 = _180.z * 12.9200000762939453125;
            }
            else
            {
                _219 = (1.05499994754791259765625 * pow(_180.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp vec3 _220 = vec3(_199, _209, _219);
            _222 = vec4(_220.x, _220.y, _220.z, _180.w);
        }
        else
        {
            _222 = _180;
        }
        _224 = Standard3DMaterialBlock.BaseColorFactor * _222;
    }
    else
    {
        _224 = Standard3DMaterialBlock.BaseColorFactor;
    }
    bool _231;
    if (Standard3DMaterialBlock.MaterialFlags.w > 0.5)
    {
        _231 = Standard3DMaterialBlock.MaterialFlags.w < 1.5;
    }
    else
    {
        _231 = false;
    }
    if (_231)
    {
        if ((_224.w - Standard3DMaterialBlock.AlphaParams.x) < 0.0)
        {
            discard;
        }
    }
    highp vec3 _241 = normalize(v_loc0);
    highp vec3 _268;
    if (Standard3DMaterialBlock.MaterialFlags.y > 0.5)
    {
        highp vec3 _248 = normalize(v_loc1.xyz);
        highp vec3 _260 = (texture(u_fragment_tex1, v_loc2).xyz * 2.0) - vec3(1.0);
        highp vec2 _264 = _260.xy * Standard3DMaterialBlock.MaterialFlags.z;
        _268 = normalize(mat3(_248, cross(_241, _248) * v_loc1.w, _241) * vec3(_264.x, _264.y, _260.z));
    }
    else
    {
        _268 = _241;
    }
    highp vec2 _284;
    if (Standard3DMaterialBlock.TextureFlags.x > 0.5)
    {
        _284 = Standard3DMaterialBlock.PbrParams.xy * texture(u_fragment_tex3, v_loc2).zy;
    }
    else
    {
        _284 = Standard3DMaterialBlock.PbrParams.xy;
    }
    highp float _286 = clamp(_284.x, 0.0, 1.0);
    highp float _288 = clamp(_284.y, 0.0500000007450580596923828125, 1.0);
    highp vec3 _293 = normalize(Standard3DLightBlock.CameraPosition.xyz - v_loc4);
    highp float _642;
    do
    {
        if (Standard3DShadowBlock.ShadowSettings.x < 0.5)
        {
            _642 = 1.0;
            break;
        }
        if (Standard3DShadowBlock.CascadeSettings.x < 0.5)
        {
            highp float _385;
            do
            {
                highp vec3 _312 = v_loc3.xyz / vec3(v_loc3.w);
                highp vec2 _318 = (vec2(_312.x, -_312.y) * 0.5) + vec2(0.5);
                bool _326;
                if (!any(lessThan(_318, vec2(0.0))))
                {
                    _326 = any(greaterThan(_318, vec2(1.0)));
                }
                else
                {
                    _326 = true;
                }
                bool _332;
                if (!_326)
                {
                    _332 = _312.z < 0.0;
                }
                else
                {
                    _332 = true;
                }
                bool _338;
                if (!_332)
                {
                    _338 = _312.z > 1.0;
                }
                else
                {
                    _338 = true;
                }
                if (_338)
                {
                    _385 = 1.0;
                    break;
                }
                highp float _342;
                int _345;
                _342 = 0.0;
                _345 = -1;
                highp float _343;
                for (; _345 <= 1; _342 = _343, _345++)
                {
                    _343 = _342;
                    for (int _353 = -1; _353 <= 1; )
                    {
                        highp vec2 _365 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                        _343 += float((_312.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_318 + (vec2(float(_353), float(_345)) * Standard3DShadowBlock.ShadowSettings.y), _365, vec2(1.0) - _365)).x);
                        _353++;
                        continue;
                    }
                }
                _385 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_342 * 0.111111111938953399658203125)));
                break;
            } while(false);
            _642 = _385;
            break;
        }
        highp float _390 = dot(v_loc4 - Standard3DLightBlock.CameraPosition.xyz, Standard3DShadowBlock.ShadowCameraForward.xyz);
        if (_390 > Standard3DShadowBlock.CascadeSplits.w)
        {
            _642 = 1.0;
            break;
        }
        int _413;
        if (_390 <= Standard3DShadowBlock.CascadeSplits.x)
        {
            _413 = 0;
        }
        else
        {
            int _412;
            if (_390 <= Standard3DShadowBlock.CascadeSplits.y)
            {
                _412 = 1;
            }
            else
            {
                _412 = (_390 <= Standard3DShadowBlock.CascadeSplits.z) ? 2 : 3;
            }
            _413 = _412;
        }
        int _414 = (_413);
        highp vec4 _420 = vec4(v_loc4, 1.0);
        highp vec4 _421 = _420 * spvWorkaroundRowMajor(Standard3DShadowBlock.CascadeMatrices[_414]);
        highp float _510;
        do
        {
            highp vec3 _427 = _421.xyz / vec3(_421.w);
            highp vec2 _433 = (vec2(_427.x, -_427.y) * 0.5) + vec2(0.5);
            bool _441;
            if (!any(lessThan(_433, vec2(0.0))))
            {
                _441 = any(greaterThan(_433, vec2(1.0)));
            }
            else
            {
                _441 = true;
            }
            bool _447;
            if (!_441)
            {
                _447 = _427.z < 0.0;
            }
            else
            {
                _447 = true;
            }
            bool _453;
            if (!_447)
            {
                _453 = _427.z > 1.0;
            }
            else
            {
                _453 = true;
            }
            if (_453)
            {
                _510 = 1.0;
                break;
            }
            highp vec2 _461 = vec2(float(_414 - 2 * (_414 / 2)), float(_414 / 2)) * 0.5;
            highp vec2 _462 = _461 + vec2(0.5);
            highp vec2 _464 = _461 + (_433 * 0.5);
            highp float _466;
            int _469;
            _466 = 0.0;
            _469 = -1;
            highp float _467;
            for (; _469 <= 1; _466 = _467, _469++)
            {
                _467 = _466;
                for (int _477 = -1; _477 <= 1; )
                {
                    highp vec2 _489 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                    _467 += float((_427.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_464 + (vec2(float(_477), float(_469)) * Standard3DShadowBlock.ShadowSettings.y), _461 + _489, _462 - _489)).x);
                    _477++;
                    continue;
                }
            }
            _510 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_466 * 0.111111111938953399658203125)));
            break;
        } while(false);
        bool _517;
        if (_414 < 3)
        {
            _517 = Standard3DShadowBlock.CascadeSettings.y > 0.0;
        }
        else
        {
            _517 = false;
        }
        highp float _641;
        if (_517)
        {
            highp float _530;
            if (_414 == 0)
            {
                _530 = Standard3DShadowBlock.ShadowCameraForward.w;
            }
            else
            {
                _530 = Standard3DShadowBlock.CascadeSplits[uint(_414 - 1)];
            }
            uint _531 = uint(_414);
            highp float _537 = (Standard3DShadowBlock.CascadeSplits[_531] - _530) * Standard3DShadowBlock.CascadeSettings.y;
            highp float _538 = isnan(9.9999997473787516355514526367188e-05) ? _537 : (isnan(_537) ? 9.9999997473787516355514526367188e-05 : max(_537, 9.9999997473787516355514526367188e-05));
            highp float _542 = clamp(((_390 - Standard3DShadowBlock.CascadeSplits[_531]) + _538) / _538, 0.0, 1.0);
            highp float _640;
            if (_542 > 0.0)
            {
                int _546 = _414 + 1;
                highp vec4 _549 = _420 * spvWorkaroundRowMajor(Standard3DShadowBlock.CascadeMatrices[_546]);
                highp float _638;
                do
                {
                    highp vec3 _555 = _549.xyz / vec3(_549.w);
                    highp vec2 _561 = (vec2(_555.x, -_555.y) * 0.5) + vec2(0.5);
                    bool _569;
                    if (!any(lessThan(_561, vec2(0.0))))
                    {
                        _569 = any(greaterThan(_561, vec2(1.0)));
                    }
                    else
                    {
                        _569 = true;
                    }
                    bool _575;
                    if (!_569)
                    {
                        _575 = _555.z < 0.0;
                    }
                    else
                    {
                        _575 = true;
                    }
                    bool _581;
                    if (!_575)
                    {
                        _581 = _555.z > 1.0;
                    }
                    else
                    {
                        _581 = true;
                    }
                    if (_581)
                    {
                        _638 = 1.0;
                        break;
                    }
                    highp vec2 _589 = vec2(float(_546 - 2 * (_546 / 2)), float(_546 / 2)) * 0.5;
                    highp vec2 _590 = _589 + vec2(0.5);
                    highp vec2 _592 = _589 + (_561 * 0.5);
                    highp float _594;
                    int _597;
                    _594 = 0.0;
                    _597 = -1;
                    highp float _595;
                    for (; _597 <= 1; _594 = _595, _597++)
                    {
                        _595 = _594;
                        for (int _605 = -1; _605 <= 1; )
                        {
                            highp vec2 _617 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                            _595 += float((_555.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_592 + (vec2(float(_605), float(_597)) * Standard3DShadowBlock.ShadowSettings.y), _589 + _617, _590 - _617)).x);
                            _605++;
                            continue;
                        }
                    }
                    _638 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_594 * 0.111111111938953399658203125)));
                    break;
                } while(false);
                _640 = mix(_510, _638, _542);
            }
            else
            {
                _640 = _510;
            }
            _641 = _640;
        }
        else
        {
            _641 = _510;
        }
        _642 = _641;
        break;
    } while(false);
    highp vec3 _647 = normalize(-Standard3DLightBlock.LightDirection.xyz);
    highp vec3 _653 = normalize(_293 + _647);
    highp float _655 = clamp(dot(_268, _647), 0.0, 1.0);
    highp float _657 = clamp(dot(_268, _293), 0.0, 1.0);
    highp vec3 _658 = vec3(_286);
    highp vec3 _659 = mix(vec3(0.039999999105930328369140625), _224.xyz, _658);
    highp vec3 _662 = vec3(1.0) - _659;
    highp vec3 _666 = _659 + (_662 * pow(1.0 - clamp(dot(_653, _293), 0.0, 1.0), 5.0));
    highp float _667 = _288 * _288;
    highp float _668 = _667 * _667;
    highp float _670 = clamp(dot(_268, _653), 0.0, 1.0);
    highp float _672 = _668 - 1.0;
    highp float _674 = ((_670 * _670) * _672) + 1.0;
    highp float _678 = _288 + 1.0;
    highp float _680 = (_678 * _678) * 0.125;
    highp float _681 = 1.0 - _680;
    highp float _684 = _657 / ((_657 * _681) + _680);
    highp float _691 = 4.0 * _657;
    highp float _692 = _691 * _655;
    highp float _697 = 1.0 - _286;
    int _706 = min(int(Standard3DPointLightBlock.PointLightMeta.x), 16);
    highp vec3 _708;
    _708 = (((((vec3(1.0) - _666) * _697) * _224.xyz) + ((_666 * ((_668 / ((3.1415927410125732421875 * _674) * _674)) * (_684 * (_655 / ((_655 * _681) + _680))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _692 : (isnan(_692) ? 9.9999997473787516355514526367188e-05 : max(_692, 9.9999997473787516355514526367188e-05))))) * (Standard3DLightBlock.Diffuse.xyz * _642)) * _655;
    for (int _711 = 0; _711 < _706; )
    {
        highp vec3 _718 = Standard3DPointLightBlock.PointLightPositionRange[_711].xyz - v_loc4;
        highp float _719 = length(_718);
        highp float _725 = clamp(1.0 - (_719 / (isnan(0.001000000047497451305389404296875) ? Standard3DPointLightBlock.PointLightPositionRange[_711].w : (isnan(Standard3DPointLightBlock.PointLightPositionRange[_711].w) ? 0.001000000047497451305389404296875 : max(Standard3DPointLightBlock.PointLightPositionRange[_711].w, 0.001000000047497451305389404296875)))), 0.0, 1.0);
        highp vec3 _736 = _718 / vec3(isnan(9.9999997473787516355514526367188e-05) ? _719 : (isnan(_719) ? 9.9999997473787516355514526367188e-05 : max(_719, 9.9999997473787516355514526367188e-05)));
        highp vec3 _738 = normalize(_293 + _736);
        highp float _740 = clamp(dot(_268, _736), 0.0, 1.0);
        highp vec3 _746 = _659 + (_662 * pow(1.0 - clamp(dot(_738, _293), 0.0, 1.0), 5.0));
        highp float _748 = clamp(dot(_268, _738), 0.0, 1.0);
        highp float _751 = ((_748 * _748) * _672) + 1.0;
        highp float _761 = _691 * _740;
        _708 += ((((((vec3(1.0) - _746) * _697) * _224.xyz) + ((_746 * ((_668 / ((3.1415927410125732421875 * _751) * _751)) * (_684 * (_740 / ((_740 * _681) + _680))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _761 : (isnan(_761) ? 9.9999997473787516355514526367188e-05 : max(_761, 9.9999997473787516355514526367188e-05))))) * ((Standard3DPointLightBlock.PointLightColorIntensity[_711].xyz * Standard3DPointLightBlock.PointLightColorIntensity[_711].w) * (_725 * _725))) * _740);
        _711++;
        continue;
    }
    highp float _785;
    if (Standard3DMaterialBlock.TextureFlags.y > 0.5)
    {
        _785 = mix(1.0, texture(u_fragment_tex4, v_loc2).x, Standard3DMaterialBlock.Emissive.w);
    }
    else
    {
        _785 = 1.0;
    }
    highp vec3 _791 = _708 + ((Standard3DLightBlock.Ambient.xyz * _224.xyz) * _785);
    highp vec3 _1022;
    if (Standard3DLightBlock.Environment.x > 0.5)
    {
        bool _799 = Standard3DLightBlock.ColorPipeline.x > 0.5;
        highp vec3 _834;
        if (_799)
        {
            _834 = _224.xyz;
        }
        else
        {
            highp float _812;
            if (_224.x <= 0.040449999272823333740234375)
            {
                _812 = _224.x * 0.077399380505084991455078125;
            }
            else
            {
                _812 = pow((_224.x + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _822;
            if (_224.y <= 0.040449999272823333740234375)
            {
                _822 = _224.y * 0.077399380505084991455078125;
            }
            else
            {
                _822 = pow((_224.y + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _832;
            if (_224.z <= 0.040449999272823333740234375)
            {
                _832 = _224.z * 0.077399380505084991455078125;
            }
            else
            {
                _832 = pow((_224.z + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _834 = vec3(_812, _822, _832);
        }
        highp vec3 _835 = mix(vec3(0.039999999105930328369140625), _834, _658);
        highp vec3 _837 = vec3(1.0 - _288);
        bvec3 _1230 = isnan(_837);
        bvec3 _1231 = isnan(_835);
        highp vec3 _1232 = max(_837, _835);
        highp vec3 _1233 = vec3(_1230.x ? _835.x : _1232.x, _1230.y ? _835.y : _1232.y, _1230.z ? _835.z : _1232.z);
        highp vec3 _845 = reflect(-_293, _268);
        highp float _848 = sin(Standard3DLightBlock.Environment.z);
        highp float _849 = cos(Standard3DLightBlock.Environment.z);
        highp float _850 = _845.x;
        highp float _852 = _845.z;
        highp vec3 _860 = normalize(vec3((_849 * _850) - (_848 * _852), _845.y, (_848 * _850) + (_849 * _852)));
        highp float _865 = (atan(_860.z, _860.x) * 0.15915493667125701904296875) + 0.5;
        highp float _872 = Standard3DLightBlock.Environment.w - 1.0;
        highp float _873 = _288 * _872;
        highp float _874 = floor(_873);
        highp float _875 = _874 + 1.0;
        highp float _879 = Standard3DLightBlock.EnvironmentSize.y + 2.0;
        highp float _883 = (acos(clamp(_860.y, -1.0, 1.0)) * 0.3183098733425140380859375) * (Standard3DLightBlock.EnvironmentSize.y - 1.0);
        highp vec4 _897 = texture(u_fragment_tex7, vec2(_865, (((_874 * _879) + 1.5) + _883) / Standard3DLightBlock.EnvironmentSize.z));
        highp vec4 _900 = texture(u_fragment_tex7, vec2(_865, ((((isnan(_872) ? _875 : (isnan(_875) ? _872 : min(_875, _872))) * _879) + 1.5) + _883) / Standard3DLightBlock.EnvironmentSize.z));
        highp vec4 _909 = texture(u_fragment_tex8, vec2(_657, _288));
        highp vec3 _922 = normalize(vec3((_849 * _268.x) - (_848 * _268.z), _268.y, (_848 * _268.x) + (_849 * _268.z)));
        highp vec4 _934 = texture(u_fragment_tex6, vec2((atan(_922.z, _922.x) * 0.15915493667125701904296875) + 0.5, acos(clamp(_922.y, -1.0, 1.0)) * 0.3183098733425140380859375));
        highp vec3 _972;
        if (_799)
        {
            _972 = _224.xyz;
        }
        else
        {
            highp float _950;
            if (_224.x <= 0.040449999272823333740234375)
            {
                _950 = _224.x * 0.077399380505084991455078125;
            }
            else
            {
                _950 = pow((_224.x + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _960;
            if (_224.y <= 0.040449999272823333740234375)
            {
                _960 = _224.y * 0.077399380505084991455078125;
            }
            else
            {
                _960 = pow((_224.y + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _970;
            if (_224.z <= 0.040449999272823333740234375)
            {
                _970 = _224.z * 0.077399380505084991455078125;
            }
            else
            {
                _970 = pow((_224.z + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _972 = vec3(_950, _960, _970);
        }
        highp vec3 _985 = ((((((vec3(1.0) - (_835 + ((vec3(_1231.x ? _837.x : _1233.x, _1231.y ? _837.y : _1233.y, _1231.z ? _837.z : _1233.z) - _835) * pow(1.0 - _657, 5.0)))) * _697) * _972) * _934.xyz) + (mix(_897.xyz, _900.xyz, vec3(_873 - _874)) * ((_835 * _909.x) + vec3(_909.y)))) * Standard3DLightBlock.Environment.y) * _785;
        highp vec3 _1020;
        if (_799)
        {
            _1020 = _985;
        }
        else
        {
            highp float _989 = _985.x;
            highp float _998;
            if (_989 <= 0.003130800090730190277099609375)
            {
                _998 = _989 * 12.9200000762939453125;
            }
            else
            {
                _998 = (1.05499994754791259765625 * pow(_989, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _999 = _985.y;
            highp float _1008;
            if (_999 <= 0.003130800090730190277099609375)
            {
                _1008 = _999 * 12.9200000762939453125;
            }
            else
            {
                _1008 = (1.05499994754791259765625 * pow(_999, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _1009 = _985.z;
            highp float _1018;
            if (_1009 <= 0.003130800090730190277099609375)
            {
                _1018 = _1009 * 12.9200000762939453125;
            }
            else
            {
                _1018 = (1.05499994754791259765625 * pow(_1009, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            _1020 = vec3(_998, _1008, _1018);
        }
        _1022 = _791 + _1020;
    }
    else
    {
        _1022 = _791;
    }
    highp vec3 _1121;
    if (Standard3DMaterialBlock.TextureFlags.z > 0.5)
    {
        highp vec4 _1034 = texture(u_fragment_tex5, v_loc2);
        bool _1044;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _1044 = Standard3DMaterialBlock.TextureFlags.w < 0.5;
        }
        else
        {
            _1044 = false;
        }
        highp vec3 _1078;
        if (_1044)
        {
            highp float _1047 = _1034.x;
            highp float _1056;
            if (_1047 <= 0.040449999272823333740234375)
            {
                _1056 = _1047 * 0.077399380505084991455078125;
            }
            else
            {
                _1056 = pow((_1047 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _1057 = _1034.y;
            highp float _1066;
            if (_1057 <= 0.040449999272823333740234375)
            {
                _1066 = _1057 * 0.077399380505084991455078125;
            }
            else
            {
                _1066 = pow((_1057 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _1067 = _1034.z;
            highp float _1076;
            if (_1067 <= 0.040449999272823333740234375)
            {
                _1076 = _1067 * 0.077399380505084991455078125;
            }
            else
            {
                _1076 = pow((_1067 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _1078 = vec3(_1056, _1066, _1076);
        }
        else
        {
            _1078 = _1034.xyz;
        }
        bool _1085;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _1085 = Standard3DMaterialBlock.TextureFlags.w > 0.5;
        }
        else
        {
            _1085 = false;
        }
        highp vec3 _1119;
        if (_1085)
        {
            highp float _1097;
            if (_1078.x <= 0.003130800090730190277099609375)
            {
                _1097 = _1078.x * 12.9200000762939453125;
            }
            else
            {
                _1097 = (1.05499994754791259765625 * pow(_1078.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _1107;
            if (_1078.y <= 0.003130800090730190277099609375)
            {
                _1107 = _1078.y * 12.9200000762939453125;
            }
            else
            {
                _1107 = (1.05499994754791259765625 * pow(_1078.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _1117;
            if (_1078.z <= 0.003130800090730190277099609375)
            {
                _1117 = _1078.z * 12.9200000762939453125;
            }
            else
            {
                _1117 = (1.05499994754791259765625 * pow(_1078.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            _1119 = vec3(_1097, _1107, _1117);
        }
        else
        {
            _1119 = _1078;
        }
        _1121 = Standard3DMaterialBlock.Emissive.xyz * _1119;
    }
    else
    {
        _1121 = Standard3DMaterialBlock.Emissive.xyz;
    }
    highp vec3 _1122 = _1022 + _1121;
    bool _1131;
    if (Standard3DShadowBlock.CascadeSettings.z > 0.5)
    {
        _1131 = Standard3DShadowBlock.CascadeSettings.x > 0.5;
    }
    else
    {
        _1131 = false;
    }
    highp vec3 _1172;
    if (_1131)
    {
        highp float _1138 = dot(v_loc4 - Standard3DLightBlock.CameraPosition.xyz, Standard3DShadowBlock.ShadowCameraForward.xyz);
        int _1156;
        if (_1138 <= Standard3DShadowBlock.CascadeSplits.x)
        {
            _1156 = 0;
        }
        else
        {
            int _1155;
            if (_1138 <= Standard3DShadowBlock.CascadeSplits.y)
            {
                _1155 = 1;
            }
            else
            {
                _1155 = (_1138 <= Standard3DShadowBlock.CascadeSplits.z) ? 2 : 3;
            }
            _1156 = _1155;
        }
        int _1157 = (_1156);
        highp vec3 _1170;
        if (_1157 == 0)
        {
            _1170 = vec3(1.0, 0.20000000298023223876953125, 0.20000000298023223876953125);
        }
        else
        {
            highp vec3 _1169;
            if (_1157 == 1)
            {
                _1169 = vec3(0.20000000298023223876953125, 1.0, 0.20000000298023223876953125);
            }
            else
            {
                bvec3 _1167 = bvec3(_1157 == 2);
                _1169 = vec3(_1167.x ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).x : vec3(1.0, 1.0, 0.20000000298023223876953125).x, _1167.y ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).y : vec3(1.0, 1.0, 0.20000000298023223876953125).y, _1167.z ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).z : vec3(1.0, 1.0, 0.20000000298023223876953125).z);
            }
            _1170 = _1169;
        }
        _1172 = mix(_1122, _1170, vec3(0.4000000059604644775390625));
    }
    else
    {
        _1172 = _1122;
    }
    highp vec3 _1180;
    if (Standard3DLightBlock.ColorPipeline.x > 0.5)
    {
        _1180 = _1172;
    }
    else
    {
        _1180 = clamp(_1172, vec3(0.0), vec3(1.0));
    }
    out_var_SV_Target0 = vec4(_1180, _224.w);
}
