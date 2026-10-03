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
    highp vec4 _214;
    if (Standard3DMaterialBlock.MaterialFlags.x > 0.5)
    {
        highp vec4 _126 = texture(u_fragment_tex0, v_loc2);
        bool _135;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _135 = Standard3DMaterialBlock.PbrParams.z < 0.5;
        }
        else
        {
            _135 = false;
        }
        highp vec4 _170;
        if (_135)
        {
            highp float _138 = _126.x;
            highp float _147;
            if (_138 <= 0.040449999272823333740234375)
            {
                _147 = _138 * 0.077399380505084991455078125;
            }
            else
            {
                _147 = pow((_138 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _148 = _126.y;
            highp float _157;
            if (_148 <= 0.040449999272823333740234375)
            {
                _157 = _148 * 0.077399380505084991455078125;
            }
            else
            {
                _157 = pow((_148 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _158 = _126.z;
            highp float _167;
            if (_158 <= 0.040449999272823333740234375)
            {
                _167 = _158 * 0.077399380505084991455078125;
            }
            else
            {
                _167 = pow((_158 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp vec3 _168 = vec3(_147, _157, _167);
            _170 = vec4(_168.x, _168.y, _168.z, _126.w);
        }
        else
        {
            _170 = _126;
        }
        bool _177;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _177 = Standard3DMaterialBlock.PbrParams.z > 0.5;
        }
        else
        {
            _177 = false;
        }
        highp vec4 _212;
        if (_177)
        {
            highp float _189;
            if (_170.x <= 0.003130800090730190277099609375)
            {
                _189 = _170.x * 12.9200000762939453125;
            }
            else
            {
                _189 = (1.05499994754791259765625 * pow(_170.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _199;
            if (_170.y <= 0.003130800090730190277099609375)
            {
                _199 = _170.y * 12.9200000762939453125;
            }
            else
            {
                _199 = (1.05499994754791259765625 * pow(_170.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _209;
            if (_170.z <= 0.003130800090730190277099609375)
            {
                _209 = _170.z * 12.9200000762939453125;
            }
            else
            {
                _209 = (1.05499994754791259765625 * pow(_170.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp vec3 _210 = vec3(_189, _199, _209);
            _212 = vec4(_210.x, _210.y, _210.z, _170.w);
        }
        else
        {
            _212 = _170;
        }
        _214 = Standard3DMaterialBlock.BaseColorFactor * _212;
    }
    else
    {
        _214 = Standard3DMaterialBlock.BaseColorFactor;
    }
    bool _221;
    if (Standard3DMaterialBlock.MaterialFlags.w > 0.5)
    {
        _221 = Standard3DMaterialBlock.MaterialFlags.w < 1.5;
    }
    else
    {
        _221 = false;
    }
    if (_221)
    {
        if ((_214.w - Standard3DMaterialBlock.AlphaParams.x) < 0.0)
        {
            discard;
        }
    }
    highp vec3 _231 = normalize(v_loc0);
    highp vec3 _258;
    if (Standard3DMaterialBlock.MaterialFlags.y > 0.5)
    {
        highp vec3 _238 = normalize(v_loc1.xyz);
        highp vec3 _250 = (texture(u_fragment_tex1, v_loc2).xyz * 2.0) - vec3(1.0);
        highp vec2 _254 = _250.xy * Standard3DMaterialBlock.MaterialFlags.z;
        _258 = normalize(mat3(_238, cross(_231, _238) * v_loc1.w, _231) * vec3(_254.x, _254.y, _250.z));
    }
    else
    {
        _258 = _231;
    }
    highp vec2 _274;
    if (Standard3DMaterialBlock.TextureFlags.x > 0.5)
    {
        _274 = Standard3DMaterialBlock.PbrParams.xy * texture(u_fragment_tex3, v_loc2).zy;
    }
    else
    {
        _274 = Standard3DMaterialBlock.PbrParams.xy;
    }
    highp float _276 = clamp(_274.x, 0.0, 1.0);
    highp float _278 = clamp(_274.y, 0.0500000007450580596923828125, 1.0);
    highp vec3 _283 = normalize(Standard3DLightBlock.CameraPosition.xyz - v_loc4);
    highp float _632;
    do
    {
        if (Standard3DShadowBlock.ShadowSettings.x < 0.5)
        {
            _632 = 1.0;
            break;
        }
        if (Standard3DShadowBlock.CascadeSettings.x < 0.5)
        {
            highp float _375;
            do
            {
                highp vec3 _302 = v_loc3.xyz / vec3(v_loc3.w);
                highp vec2 _308 = (vec2(_302.x, -_302.y) * 0.5) + vec2(0.5);
                bool _316;
                if (!any(lessThan(_308, vec2(0.0))))
                {
                    _316 = any(greaterThan(_308, vec2(1.0)));
                }
                else
                {
                    _316 = true;
                }
                bool _322;
                if (!_316)
                {
                    _322 = _302.z < 0.0;
                }
                else
                {
                    _322 = true;
                }
                bool _328;
                if (!_322)
                {
                    _328 = _302.z > 1.0;
                }
                else
                {
                    _328 = true;
                }
                if (_328)
                {
                    _375 = 1.0;
                    break;
                }
                highp float _332;
                int _335;
                _332 = 0.0;
                _335 = -1;
                highp float _333;
                for (; _335 <= 1; _332 = _333, _335++)
                {
                    _333 = _332;
                    for (int _343 = -1; _343 <= 1; )
                    {
                        highp vec2 _355 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                        _333 += float((_302.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_308 + (vec2(float(_343), float(_335)) * Standard3DShadowBlock.ShadowSettings.y), _355, vec2(1.0) - _355)).x);
                        _343++;
                        continue;
                    }
                }
                _375 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_332 * 0.111111111938953399658203125)));
                break;
            } while(false);
            _632 = _375;
            break;
        }
        highp float _380 = dot(v_loc4 - Standard3DLightBlock.CameraPosition.xyz, Standard3DShadowBlock.ShadowCameraForward.xyz);
        if (_380 > Standard3DShadowBlock.CascadeSplits.w)
        {
            _632 = 1.0;
            break;
        }
        int _403;
        if (_380 <= Standard3DShadowBlock.CascadeSplits.x)
        {
            _403 = 0;
        }
        else
        {
            int _402;
            if (_380 <= Standard3DShadowBlock.CascadeSplits.y)
            {
                _402 = 1;
            }
            else
            {
                _402 = (_380 <= Standard3DShadowBlock.CascadeSplits.z) ? 2 : 3;
            }
            _403 = _402;
        }
        int _404 = (_403);
        highp vec4 _410 = vec4(v_loc4, 1.0);
        highp vec4 _411 = _410 * spvWorkaroundRowMajor(Standard3DShadowBlock.CascadeMatrices[_404]);
        highp float _500;
        do
        {
            highp vec3 _417 = _411.xyz / vec3(_411.w);
            highp vec2 _423 = (vec2(_417.x, -_417.y) * 0.5) + vec2(0.5);
            bool _431;
            if (!any(lessThan(_423, vec2(0.0))))
            {
                _431 = any(greaterThan(_423, vec2(1.0)));
            }
            else
            {
                _431 = true;
            }
            bool _437;
            if (!_431)
            {
                _437 = _417.z < 0.0;
            }
            else
            {
                _437 = true;
            }
            bool _443;
            if (!_437)
            {
                _443 = _417.z > 1.0;
            }
            else
            {
                _443 = true;
            }
            if (_443)
            {
                _500 = 1.0;
                break;
            }
            highp vec2 _451 = vec2(float(_404 - 2 * (_404 / 2)), float(_404 / 2)) * 0.5;
            highp vec2 _452 = _451 + vec2(0.5);
            highp vec2 _454 = _451 + (_423 * 0.5);
            highp float _456;
            int _459;
            _456 = 0.0;
            _459 = -1;
            highp float _457;
            for (; _459 <= 1; _456 = _457, _459++)
            {
                _457 = _456;
                for (int _467 = -1; _467 <= 1; )
                {
                    highp vec2 _479 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                    _457 += float((_417.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_454 + (vec2(float(_467), float(_459)) * Standard3DShadowBlock.ShadowSettings.y), _451 + _479, _452 - _479)).x);
                    _467++;
                    continue;
                }
            }
            _500 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_456 * 0.111111111938953399658203125)));
            break;
        } while(false);
        bool _507;
        if (_404 < 3)
        {
            _507 = Standard3DShadowBlock.CascadeSettings.y > 0.0;
        }
        else
        {
            _507 = false;
        }
        highp float _631;
        if (_507)
        {
            highp float _520;
            if (_404 == 0)
            {
                _520 = Standard3DShadowBlock.ShadowCameraForward.w;
            }
            else
            {
                _520 = Standard3DShadowBlock.CascadeSplits[uint(_404 - 1)];
            }
            uint _521 = uint(_404);
            highp float _527 = (Standard3DShadowBlock.CascadeSplits[_521] - _520) * Standard3DShadowBlock.CascadeSettings.y;
            highp float _528 = isnan(9.9999997473787516355514526367188e-05) ? _527 : (isnan(_527) ? 9.9999997473787516355514526367188e-05 : max(_527, 9.9999997473787516355514526367188e-05));
            highp float _532 = clamp(((_380 - Standard3DShadowBlock.CascadeSplits[_521]) + _528) / _528, 0.0, 1.0);
            highp float _630;
            if (_532 > 0.0)
            {
                int _536 = _404 + 1;
                highp vec4 _539 = _410 * spvWorkaroundRowMajor(Standard3DShadowBlock.CascadeMatrices[_536]);
                highp float _628;
                do
                {
                    highp vec3 _545 = _539.xyz / vec3(_539.w);
                    highp vec2 _551 = (vec2(_545.x, -_545.y) * 0.5) + vec2(0.5);
                    bool _559;
                    if (!any(lessThan(_551, vec2(0.0))))
                    {
                        _559 = any(greaterThan(_551, vec2(1.0)));
                    }
                    else
                    {
                        _559 = true;
                    }
                    bool _565;
                    if (!_559)
                    {
                        _565 = _545.z < 0.0;
                    }
                    else
                    {
                        _565 = true;
                    }
                    bool _571;
                    if (!_565)
                    {
                        _571 = _545.z > 1.0;
                    }
                    else
                    {
                        _571 = true;
                    }
                    if (_571)
                    {
                        _628 = 1.0;
                        break;
                    }
                    highp vec2 _579 = vec2(float(_536 - 2 * (_536 / 2)), float(_536 / 2)) * 0.5;
                    highp vec2 _580 = _579 + vec2(0.5);
                    highp vec2 _582 = _579 + (_551 * 0.5);
                    highp float _584;
                    int _587;
                    _584 = 0.0;
                    _587 = -1;
                    highp float _585;
                    for (; _587 <= 1; _584 = _585, _587++)
                    {
                        _585 = _584;
                        for (int _595 = -1; _595 <= 1; )
                        {
                            highp vec2 _607 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                            _585 += float((_545.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_582 + (vec2(float(_595), float(_587)) * Standard3DShadowBlock.ShadowSettings.y), _579 + _607, _580 - _607)).x);
                            _595++;
                            continue;
                        }
                    }
                    _628 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_584 * 0.111111111938953399658203125)));
                    break;
                } while(false);
                _630 = mix(_500, _628, _532);
            }
            else
            {
                _630 = _500;
            }
            _631 = _630;
        }
        else
        {
            _631 = _500;
        }
        _632 = _631;
        break;
    } while(false);
    highp vec3 _637 = normalize(-Standard3DLightBlock.LightDirection.xyz);
    highp vec3 _643 = normalize(_283 + _637);
    highp float _645 = clamp(dot(_258, _637), 0.0, 1.0);
    highp float _647 = clamp(dot(_258, _283), 0.0, 1.0);
    highp vec3 _649 = mix(vec3(0.039999999105930328369140625), _214.xyz, vec3(_276));
    highp vec3 _652 = vec3(1.0) - _649;
    highp vec3 _656 = _649 + (_652 * pow(1.0 - clamp(dot(_643, _283), 0.0, 1.0), 5.0));
    highp float _657 = _278 * _278;
    highp float _658 = _657 * _657;
    highp float _660 = clamp(dot(_258, _643), 0.0, 1.0);
    highp float _662 = _658 - 1.0;
    highp float _664 = ((_660 * _660) * _662) + 1.0;
    highp float _668 = _278 + 1.0;
    highp float _670 = (_668 * _668) * 0.125;
    highp float _671 = 1.0 - _670;
    highp float _674 = _647 / ((_647 * _671) + _670);
    highp float _681 = 4.0 * _647;
    highp float _682 = _681 * _645;
    highp float _687 = 1.0 - _276;
    int _696 = min(int(Standard3DPointLightBlock.PointLightMeta.x), 16);
    highp vec3 _698;
    _698 = (((((vec3(1.0) - _656) * _687) * _214.xyz) + ((_656 * ((_658 / ((3.1415927410125732421875 * _664) * _664)) * (_674 * (_645 / ((_645 * _671) + _670))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _682 : (isnan(_682) ? 9.9999997473787516355514526367188e-05 : max(_682, 9.9999997473787516355514526367188e-05))))) * (Standard3DLightBlock.Diffuse.xyz * _632)) * _645;
    for (int _701 = 0; _701 < _696; )
    {
        highp vec3 _708 = Standard3DPointLightBlock.PointLightPositionRange[_701].xyz - v_loc4;
        highp float _709 = length(_708);
        highp float _715 = clamp(1.0 - (_709 / (isnan(0.001000000047497451305389404296875) ? Standard3DPointLightBlock.PointLightPositionRange[_701].w : (isnan(Standard3DPointLightBlock.PointLightPositionRange[_701].w) ? 0.001000000047497451305389404296875 : max(Standard3DPointLightBlock.PointLightPositionRange[_701].w, 0.001000000047497451305389404296875)))), 0.0, 1.0);
        highp vec3 _726 = _708 / vec3(isnan(9.9999997473787516355514526367188e-05) ? _709 : (isnan(_709) ? 9.9999997473787516355514526367188e-05 : max(_709, 9.9999997473787516355514526367188e-05)));
        highp vec3 _728 = normalize(_283 + _726);
        highp float _730 = clamp(dot(_258, _726), 0.0, 1.0);
        highp vec3 _736 = _649 + (_652 * pow(1.0 - clamp(dot(_728, _283), 0.0, 1.0), 5.0));
        highp float _738 = clamp(dot(_258, _728), 0.0, 1.0);
        highp float _741 = ((_738 * _738) * _662) + 1.0;
        highp float _751 = _681 * _730;
        _698 += ((((((vec3(1.0) - _736) * _687) * _214.xyz) + ((_736 * ((_658 / ((3.1415927410125732421875 * _741) * _741)) * (_674 * (_730 / ((_730 * _671) + _670))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _751 : (isnan(_751) ? 9.9999997473787516355514526367188e-05 : max(_751, 9.9999997473787516355514526367188e-05))))) * ((Standard3DPointLightBlock.PointLightColorIntensity[_701].xyz * Standard3DPointLightBlock.PointLightColorIntensity[_701].w) * (_715 * _715))) * _730);
        _701++;
        continue;
    }
    highp float _775;
    if (Standard3DMaterialBlock.TextureFlags.y > 0.5)
    {
        _775 = mix(1.0, texture(u_fragment_tex4, v_loc2).x, Standard3DMaterialBlock.Emissive.w);
    }
    else
    {
        _775 = 1.0;
    }
    highp vec3 _880;
    if (Standard3DMaterialBlock.TextureFlags.z > 0.5)
    {
        highp vec4 _793 = texture(u_fragment_tex5, v_loc2);
        bool _803;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _803 = Standard3DMaterialBlock.TextureFlags.w < 0.5;
        }
        else
        {
            _803 = false;
        }
        highp vec3 _837;
        if (_803)
        {
            highp float _806 = _793.x;
            highp float _815;
            if (_806 <= 0.040449999272823333740234375)
            {
                _815 = _806 * 0.077399380505084991455078125;
            }
            else
            {
                _815 = pow((_806 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _816 = _793.y;
            highp float _825;
            if (_816 <= 0.040449999272823333740234375)
            {
                _825 = _816 * 0.077399380505084991455078125;
            }
            else
            {
                _825 = pow((_816 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _826 = _793.z;
            highp float _835;
            if (_826 <= 0.040449999272823333740234375)
            {
                _835 = _826 * 0.077399380505084991455078125;
            }
            else
            {
                _835 = pow((_826 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _837 = vec3(_815, _825, _835);
        }
        else
        {
            _837 = _793.xyz;
        }
        bool _844;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _844 = Standard3DMaterialBlock.TextureFlags.w > 0.5;
        }
        else
        {
            _844 = false;
        }
        highp vec3 _878;
        if (_844)
        {
            highp float _856;
            if (_837.x <= 0.003130800090730190277099609375)
            {
                _856 = _837.x * 12.9200000762939453125;
            }
            else
            {
                _856 = (1.05499994754791259765625 * pow(_837.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _866;
            if (_837.y <= 0.003130800090730190277099609375)
            {
                _866 = _837.y * 12.9200000762939453125;
            }
            else
            {
                _866 = (1.05499994754791259765625 * pow(_837.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _876;
            if (_837.z <= 0.003130800090730190277099609375)
            {
                _876 = _837.z * 12.9200000762939453125;
            }
            else
            {
                _876 = (1.05499994754791259765625 * pow(_837.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            _878 = vec3(_856, _866, _876);
        }
        else
        {
            _878 = _837;
        }
        _880 = Standard3DMaterialBlock.Emissive.xyz * _878;
    }
    else
    {
        _880 = Standard3DMaterialBlock.Emissive.xyz;
    }
    highp vec3 _881 = (_698 + ((Standard3DLightBlock.Ambient.xyz * _214.xyz) * _775)) + _880;
    bool _890;
    if (Standard3DShadowBlock.CascadeSettings.z > 0.5)
    {
        _890 = Standard3DShadowBlock.CascadeSettings.x > 0.5;
    }
    else
    {
        _890 = false;
    }
    highp vec3 _931;
    if (_890)
    {
        highp float _897 = dot(v_loc4 - Standard3DLightBlock.CameraPosition.xyz, Standard3DShadowBlock.ShadowCameraForward.xyz);
        int _915;
        if (_897 <= Standard3DShadowBlock.CascadeSplits.x)
        {
            _915 = 0;
        }
        else
        {
            int _914;
            if (_897 <= Standard3DShadowBlock.CascadeSplits.y)
            {
                _914 = 1;
            }
            else
            {
                _914 = (_897 <= Standard3DShadowBlock.CascadeSplits.z) ? 2 : 3;
            }
            _915 = _914;
        }
        int _916 = (_915);
        highp vec3 _929;
        if (_916 == 0)
        {
            _929 = vec3(1.0, 0.20000000298023223876953125, 0.20000000298023223876953125);
        }
        else
        {
            highp vec3 _928;
            if (_916 == 1)
            {
                _928 = vec3(0.20000000298023223876953125, 1.0, 0.20000000298023223876953125);
            }
            else
            {
                bvec3 _926 = bvec3(_916 == 2);
                _928 = vec3(_926.x ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).x : vec3(1.0, 1.0, 0.20000000298023223876953125).x, _926.y ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).y : vec3(1.0, 1.0, 0.20000000298023223876953125).y, _926.z ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).z : vec3(1.0, 1.0, 0.20000000298023223876953125).z);
            }
            _929 = _928;
        }
        _931 = mix(_881, _929, vec3(0.4000000059604644775390625));
    }
    else
    {
        _931 = _881;
    }
    highp vec3 _939;
    if (Standard3DLightBlock.ColorPipeline.x > 0.5)
    {
        _939 = _931;
    }
    else
    {
        _939 = clamp(_931, vec3(0.0), vec3(1.0));
    }
    out_var_SV_Target0 = vec4(_939, _214.w);
}
