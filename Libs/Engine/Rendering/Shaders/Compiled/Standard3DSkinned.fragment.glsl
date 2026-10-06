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
    highp vec4 SpotLightMeta;
    highp vec4 SpotLightPositionRange[16];
    highp vec4 SpotLightColorIntensity[16];
    highp vec4 SpotLightDirectionOuter[16];
    highp vec4 SpotLightInner[16];
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
    highp vec4 _227;
    if (Standard3DMaterialBlock.MaterialFlags.x > 0.5)
    {
        highp vec4 _139 = texture(u_fragment_tex0, v_loc2);
        bool _148;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _148 = Standard3DMaterialBlock.PbrParams.z < 0.5;
        }
        else
        {
            _148 = false;
        }
        highp vec4 _183;
        if (_148)
        {
            highp float _151 = _139.x;
            highp float _160;
            if (_151 <= 0.040449999272823333740234375)
            {
                _160 = _151 * 0.077399380505084991455078125;
            }
            else
            {
                _160 = pow((_151 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _161 = _139.y;
            highp float _170;
            if (_161 <= 0.040449999272823333740234375)
            {
                _170 = _161 * 0.077399380505084991455078125;
            }
            else
            {
                _170 = pow((_161 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _171 = _139.z;
            highp float _180;
            if (_171 <= 0.040449999272823333740234375)
            {
                _180 = _171 * 0.077399380505084991455078125;
            }
            else
            {
                _180 = pow((_171 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp vec3 _181 = vec3(_160, _170, _180);
            _183 = vec4(_181.x, _181.y, _181.z, _139.w);
        }
        else
        {
            _183 = _139;
        }
        bool _190;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _190 = Standard3DMaterialBlock.PbrParams.z > 0.5;
        }
        else
        {
            _190 = false;
        }
        highp vec4 _225;
        if (_190)
        {
            highp float _202;
            if (_183.x <= 0.003130800090730190277099609375)
            {
                _202 = _183.x * 12.9200000762939453125;
            }
            else
            {
                _202 = (1.05499994754791259765625 * pow(_183.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _212;
            if (_183.y <= 0.003130800090730190277099609375)
            {
                _212 = _183.y * 12.9200000762939453125;
            }
            else
            {
                _212 = (1.05499994754791259765625 * pow(_183.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _222;
            if (_183.z <= 0.003130800090730190277099609375)
            {
                _222 = _183.z * 12.9200000762939453125;
            }
            else
            {
                _222 = (1.05499994754791259765625 * pow(_183.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp vec3 _223 = vec3(_202, _212, _222);
            _225 = vec4(_223.x, _223.y, _223.z, _183.w);
        }
        else
        {
            _225 = _183;
        }
        _227 = Standard3DMaterialBlock.BaseColorFactor * _225;
    }
    else
    {
        _227 = Standard3DMaterialBlock.BaseColorFactor;
    }
    bool _234;
    if (Standard3DMaterialBlock.MaterialFlags.w > 0.5)
    {
        _234 = Standard3DMaterialBlock.MaterialFlags.w < 1.5;
    }
    else
    {
        _234 = false;
    }
    if (_234)
    {
        if ((_227.w - Standard3DMaterialBlock.AlphaParams.x) < 0.0)
        {
            discard;
        }
    }
    highp vec3 _244 = normalize(v_loc0);
    highp vec3 _271;
    if (Standard3DMaterialBlock.MaterialFlags.y > 0.5)
    {
        highp vec3 _251 = normalize(v_loc1.xyz);
        highp vec3 _263 = (texture(u_fragment_tex1, v_loc2).xyz * 2.0) - vec3(1.0);
        highp vec2 _267 = _263.xy * Standard3DMaterialBlock.MaterialFlags.z;
        _271 = normalize(mat3(_251, cross(_244, _251) * v_loc1.w, _244) * vec3(_267.x, _267.y, _263.z));
    }
    else
    {
        _271 = _244;
    }
    highp vec2 _287;
    if (Standard3DMaterialBlock.TextureFlags.x > 0.5)
    {
        _287 = Standard3DMaterialBlock.PbrParams.xy * texture(u_fragment_tex3, v_loc2).zy;
    }
    else
    {
        _287 = Standard3DMaterialBlock.PbrParams.xy;
    }
    highp float _289 = clamp(_287.x, 0.0, 1.0);
    highp float _291 = clamp(_287.y, 0.0500000007450580596923828125, 1.0);
    highp vec3 _296 = normalize(Standard3DLightBlock.CameraPosition.xyz - v_loc4);
    highp float _645;
    do
    {
        if (Standard3DShadowBlock.ShadowSettings.x < 0.5)
        {
            _645 = 1.0;
            break;
        }
        if (Standard3DShadowBlock.CascadeSettings.x < 0.5)
        {
            highp float _388;
            do
            {
                highp vec3 _315 = v_loc3.xyz / vec3(v_loc3.w);
                highp vec2 _321 = (vec2(_315.x, -_315.y) * 0.5) + vec2(0.5);
                bool _329;
                if (!any(lessThan(_321, vec2(0.0))))
                {
                    _329 = any(greaterThan(_321, vec2(1.0)));
                }
                else
                {
                    _329 = true;
                }
                bool _335;
                if (!_329)
                {
                    _335 = _315.z < 0.0;
                }
                else
                {
                    _335 = true;
                }
                bool _341;
                if (!_335)
                {
                    _341 = _315.z > 1.0;
                }
                else
                {
                    _341 = true;
                }
                if (_341)
                {
                    _388 = 1.0;
                    break;
                }
                highp float _345;
                int _348;
                _345 = 0.0;
                _348 = -1;
                highp float _346;
                for (; _348 <= 1; _345 = _346, _348++)
                {
                    _346 = _345;
                    for (int _356 = -1; _356 <= 1; )
                    {
                        highp vec2 _368 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                        _346 += float((_315.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_321 + (vec2(float(_356), float(_348)) * Standard3DShadowBlock.ShadowSettings.y), _368, vec2(1.0) - _368)).x);
                        _356++;
                        continue;
                    }
                }
                _388 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_345 * 0.111111111938953399658203125)));
                break;
            } while(false);
            _645 = _388;
            break;
        }
        highp float _393 = dot(v_loc4 - Standard3DLightBlock.CameraPosition.xyz, Standard3DShadowBlock.ShadowCameraForward.xyz);
        if (_393 > Standard3DShadowBlock.CascadeSplits.w)
        {
            _645 = 1.0;
            break;
        }
        int _416;
        if (_393 <= Standard3DShadowBlock.CascadeSplits.x)
        {
            _416 = 0;
        }
        else
        {
            int _415;
            if (_393 <= Standard3DShadowBlock.CascadeSplits.y)
            {
                _415 = 1;
            }
            else
            {
                _415 = (_393 <= Standard3DShadowBlock.CascadeSplits.z) ? 2 : 3;
            }
            _416 = _415;
        }
        int _417 = (_416);
        highp vec4 _423 = vec4(v_loc4, 1.0);
        highp vec4 _424 = _423 * spvWorkaroundRowMajor(Standard3DShadowBlock.CascadeMatrices[_417]);
        highp float _513;
        do
        {
            highp vec3 _430 = _424.xyz / vec3(_424.w);
            highp vec2 _436 = (vec2(_430.x, -_430.y) * 0.5) + vec2(0.5);
            bool _444;
            if (!any(lessThan(_436, vec2(0.0))))
            {
                _444 = any(greaterThan(_436, vec2(1.0)));
            }
            else
            {
                _444 = true;
            }
            bool _450;
            if (!_444)
            {
                _450 = _430.z < 0.0;
            }
            else
            {
                _450 = true;
            }
            bool _456;
            if (!_450)
            {
                _456 = _430.z > 1.0;
            }
            else
            {
                _456 = true;
            }
            if (_456)
            {
                _513 = 1.0;
                break;
            }
            highp vec2 _464 = vec2(float(_417 - 2 * (_417 / 2)), float(_417 / 2)) * 0.5;
            highp vec2 _465 = _464 + vec2(0.5);
            highp vec2 _467 = _464 + (_436 * 0.5);
            highp float _469;
            int _472;
            _469 = 0.0;
            _472 = -1;
            highp float _470;
            for (; _472 <= 1; _469 = _470, _472++)
            {
                _470 = _469;
                for (int _480 = -1; _480 <= 1; )
                {
                    highp vec2 _492 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                    _470 += float((_430.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_467 + (vec2(float(_480), float(_472)) * Standard3DShadowBlock.ShadowSettings.y), _464 + _492, _465 - _492)).x);
                    _480++;
                    continue;
                }
            }
            _513 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_469 * 0.111111111938953399658203125)));
            break;
        } while(false);
        bool _520;
        if (_417 < 3)
        {
            _520 = Standard3DShadowBlock.CascadeSettings.y > 0.0;
        }
        else
        {
            _520 = false;
        }
        highp float _644;
        if (_520)
        {
            highp float _533;
            if (_417 == 0)
            {
                _533 = Standard3DShadowBlock.ShadowCameraForward.w;
            }
            else
            {
                _533 = Standard3DShadowBlock.CascadeSplits[uint(_417 - 1)];
            }
            uint _534 = uint(_417);
            highp float _540 = (Standard3DShadowBlock.CascadeSplits[_534] - _533) * Standard3DShadowBlock.CascadeSettings.y;
            highp float _541 = isnan(9.9999997473787516355514526367188e-05) ? _540 : (isnan(_540) ? 9.9999997473787516355514526367188e-05 : max(_540, 9.9999997473787516355514526367188e-05));
            highp float _545 = clamp(((_393 - Standard3DShadowBlock.CascadeSplits[_534]) + _541) / _541, 0.0, 1.0);
            highp float _643;
            if (_545 > 0.0)
            {
                int _549 = _417 + 1;
                highp vec4 _552 = _423 * spvWorkaroundRowMajor(Standard3DShadowBlock.CascadeMatrices[_549]);
                highp float _641;
                do
                {
                    highp vec3 _558 = _552.xyz / vec3(_552.w);
                    highp vec2 _564 = (vec2(_558.x, -_558.y) * 0.5) + vec2(0.5);
                    bool _572;
                    if (!any(lessThan(_564, vec2(0.0))))
                    {
                        _572 = any(greaterThan(_564, vec2(1.0)));
                    }
                    else
                    {
                        _572 = true;
                    }
                    bool _578;
                    if (!_572)
                    {
                        _578 = _558.z < 0.0;
                    }
                    else
                    {
                        _578 = true;
                    }
                    bool _584;
                    if (!_578)
                    {
                        _584 = _558.z > 1.0;
                    }
                    else
                    {
                        _584 = true;
                    }
                    if (_584)
                    {
                        _641 = 1.0;
                        break;
                    }
                    highp vec2 _592 = vec2(float(_549 - 2 * (_549 / 2)), float(_549 / 2)) * 0.5;
                    highp vec2 _593 = _592 + vec2(0.5);
                    highp vec2 _595 = _592 + (_564 * 0.5);
                    highp float _597;
                    int _600;
                    _597 = 0.0;
                    _600 = -1;
                    highp float _598;
                    for (; _600 <= 1; _597 = _598, _600++)
                    {
                        _598 = _597;
                        for (int _608 = -1; _608 <= 1; )
                        {
                            highp vec2 _620 = vec2(Standard3DShadowBlock.ShadowSettings.y * 0.5);
                            _598 += float((_558.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, clamp(_595 + (vec2(float(_608), float(_600)) * Standard3DShadowBlock.ShadowSettings.y), _592 + _620, _593 - _620)).x);
                            _608++;
                            continue;
                        }
                    }
                    _641 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_597 * 0.111111111938953399658203125)));
                    break;
                } while(false);
                _643 = mix(_513, _641, _545);
            }
            else
            {
                _643 = _513;
            }
            _644 = _643;
        }
        else
        {
            _644 = _513;
        }
        _645 = _644;
        break;
    } while(false);
    highp vec3 _650 = normalize(-Standard3DLightBlock.LightDirection.xyz);
    highp vec3 _656 = normalize(_296 + _650);
    highp float _658 = clamp(dot(_271, _650), 0.0, 1.0);
    highp float _660 = clamp(dot(_271, _296), 0.0, 1.0);
    highp vec3 _661 = vec3(_289);
    highp vec3 _662 = mix(vec3(0.039999999105930328369140625), _227.xyz, _661);
    highp vec3 _665 = vec3(1.0) - _662;
    highp vec3 _669 = _662 + (_665 * pow(1.0 - clamp(dot(_656, _296), 0.0, 1.0), 5.0));
    highp float _670 = _291 * _291;
    highp float _671 = _670 * _670;
    highp float _673 = clamp(dot(_271, _656), 0.0, 1.0);
    highp float _675 = _671 - 1.0;
    highp float _677 = ((_673 * _673) * _675) + 1.0;
    highp float _681 = _291 + 1.0;
    highp float _683 = (_681 * _681) * 0.125;
    highp float _684 = 1.0 - _683;
    highp float _687 = _660 / ((_660 * _684) + _683);
    highp float _694 = 4.0 * _660;
    highp float _695 = _694 * _658;
    highp float _700 = 1.0 - _289;
    int _709 = min(int(Standard3DPointLightBlock.PointLightMeta.x), 16);
    highp vec3 _711;
    _711 = (((((vec3(1.0) - _669) * _700) * _227.xyz) + ((_669 * ((_671 / ((3.1415927410125732421875 * _677) * _677)) * (_687 * (_658 / ((_658 * _684) + _683))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _695 : (isnan(_695) ? 9.9999997473787516355514526367188e-05 : max(_695, 9.9999997473787516355514526367188e-05))))) * (Standard3DLightBlock.Diffuse.xyz * _645)) * _658;
    for (int _714 = 0; _714 < _709; )
    {
        highp vec3 _721 = Standard3DPointLightBlock.PointLightPositionRange[_714].xyz - v_loc4;
        highp float _722 = length(_721);
        highp float _728 = clamp(1.0 - (_722 / (isnan(0.001000000047497451305389404296875) ? Standard3DPointLightBlock.PointLightPositionRange[_714].w : (isnan(Standard3DPointLightBlock.PointLightPositionRange[_714].w) ? 0.001000000047497451305389404296875 : max(Standard3DPointLightBlock.PointLightPositionRange[_714].w, 0.001000000047497451305389404296875)))), 0.0, 1.0);
        highp vec3 _739 = _721 / vec3(isnan(9.9999997473787516355514526367188e-05) ? _722 : (isnan(_722) ? 9.9999997473787516355514526367188e-05 : max(_722, 9.9999997473787516355514526367188e-05)));
        highp vec3 _741 = normalize(_296 + _739);
        highp float _743 = clamp(dot(_271, _739), 0.0, 1.0);
        highp vec3 _749 = _662 + (_665 * pow(1.0 - clamp(dot(_741, _296), 0.0, 1.0), 5.0));
        highp float _751 = clamp(dot(_271, _741), 0.0, 1.0);
        highp float _754 = ((_751 * _751) * _675) + 1.0;
        highp float _764 = _694 * _743;
        _711 += ((((((vec3(1.0) - _749) * _700) * _227.xyz) + ((_749 * ((_671 / ((3.1415927410125732421875 * _754) * _754)) * (_687 * (_743 / ((_743 * _684) + _683))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _764 : (isnan(_764) ? 9.9999997473787516355514526367188e-05 : max(_764, 9.9999997473787516355514526367188e-05))))) * ((Standard3DPointLightBlock.PointLightColorIntensity[_714].xyz * Standard3DPointLightBlock.PointLightColorIntensity[_714].w) * (_728 * _728))) * _743);
        _714++;
        continue;
    }
    int _777 = min(int(Standard3DPointLightBlock.SpotLightMeta.x), 16);
    highp vec3 _779;
    _779 = _711;
    highp vec3 _780;
    for (int _782 = 0; _782 < _777; _779 = _780, _782++)
    {
        highp vec3 _790 = Standard3DPointLightBlock.SpotLightPositionRange[_782].xyz - v_loc4;
        highp float _791 = length(_790);
        highp vec3 _794 = _790 / vec3(isnan(9.9999997473787516355514526367188e-05) ? _791 : (isnan(_791) ? 9.9999997473787516355514526367188e-05 : max(_791, 9.9999997473787516355514526367188e-05)));
        highp float _799 = dot(-_794, Standard3DPointLightBlock.SpotLightDirectionOuter[_782].xyz);
        highp float _804 = Standard3DPointLightBlock.SpotLightInner[_782].x - Standard3DPointLightBlock.SpotLightDirectionOuter[_782].w;
        highp float _813;
        if (_804 > 9.9999997473787516355514526367188e-06)
        {
            _813 = clamp((_799 - Standard3DPointLightBlock.SpotLightDirectionOuter[_782].w) / _804, 0.0, 1.0);
        }
        else
        {
            _813 = step(Standard3DPointLightBlock.SpotLightDirectionOuter[_782].w, _799);
        }
        highp float _823 = clamp(1.0 - (_791 / (isnan(0.001000000047497451305389404296875) ? Standard3DPointLightBlock.SpotLightPositionRange[_782].w : (isnan(Standard3DPointLightBlock.SpotLightPositionRange[_782].w) ? 0.001000000047497451305389404296875 : max(Standard3DPointLightBlock.SpotLightPositionRange[_782].w, 0.001000000047497451305389404296875)))), 0.0, 1.0);
        highp vec3 _834 = normalize(_296 + _794);
        highp float _836 = clamp(dot(_271, _794), 0.0, 1.0);
        highp vec3 _842 = _662 + (_665 * pow(1.0 - clamp(dot(_834, _296), 0.0, 1.0), 5.0));
        highp float _844 = clamp(dot(_271, _834), 0.0, 1.0);
        highp float _847 = ((_844 * _844) * _675) + 1.0;
        highp float _857 = _694 * _836;
        _780 = _779 + ((((((vec3(1.0) - _842) * _700) * _227.xyz) + ((_842 * ((_671 / ((3.1415927410125732421875 * _847) * _847)) * (_687 * (_836 / ((_836 * _684) + _683))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _857 : (isnan(_857) ? 9.9999997473787516355514526367188e-05 : max(_857, 9.9999997473787516355514526367188e-05))))) * ((((Standard3DPointLightBlock.SpotLightColorIntensity[_782].xyz * Standard3DPointLightBlock.SpotLightColorIntensity[_782].w) * _823) * _823) * ((_813 * _813) * (3.0 - (2.0 * _813))))) * _836);
    }
    highp float _881;
    if (Standard3DMaterialBlock.TextureFlags.y > 0.5)
    {
        _881 = mix(1.0, texture(u_fragment_tex4, v_loc2).x, Standard3DMaterialBlock.Emissive.w);
    }
    else
    {
        _881 = 1.0;
    }
    highp vec3 _887 = _779 + ((Standard3DLightBlock.Ambient.xyz * _227.xyz) * _881);
    highp vec3 _1118;
    if (Standard3DLightBlock.Environment.x > 0.5)
    {
        bool _895 = Standard3DLightBlock.ColorPipeline.x > 0.5;
        highp vec3 _930;
        if (_895)
        {
            _930 = _227.xyz;
        }
        else
        {
            highp float _908;
            if (_227.x <= 0.040449999272823333740234375)
            {
                _908 = _227.x * 0.077399380505084991455078125;
            }
            else
            {
                _908 = pow((_227.x + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _918;
            if (_227.y <= 0.040449999272823333740234375)
            {
                _918 = _227.y * 0.077399380505084991455078125;
            }
            else
            {
                _918 = pow((_227.y + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _928;
            if (_227.z <= 0.040449999272823333740234375)
            {
                _928 = _227.z * 0.077399380505084991455078125;
            }
            else
            {
                _928 = pow((_227.z + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _930 = vec3(_908, _918, _928);
        }
        highp vec3 _931 = mix(vec3(0.039999999105930328369140625), _930, _661);
        highp vec3 _933 = vec3(1.0 - _291);
        bvec3 _1341 = isnan(_933);
        bvec3 _1342 = isnan(_931);
        highp vec3 _1343 = max(_933, _931);
        highp vec3 _1344 = vec3(_1341.x ? _931.x : _1343.x, _1341.y ? _931.y : _1343.y, _1341.z ? _931.z : _1343.z);
        highp vec3 _941 = reflect(-_296, _271);
        highp float _944 = sin(Standard3DLightBlock.Environment.z);
        highp float _945 = cos(Standard3DLightBlock.Environment.z);
        highp float _946 = _941.x;
        highp float _948 = _941.z;
        highp vec3 _956 = normalize(vec3((_945 * _946) - (_944 * _948), _941.y, (_944 * _946) + (_945 * _948)));
        highp float _961 = (atan(_956.z, _956.x) * 0.15915493667125701904296875) + 0.5;
        highp float _968 = Standard3DLightBlock.Environment.w - 1.0;
        highp float _969 = _291 * _968;
        highp float _970 = floor(_969);
        highp float _971 = _970 + 1.0;
        highp float _975 = Standard3DLightBlock.EnvironmentSize.y + 2.0;
        highp float _979 = (acos(clamp(_956.y, -1.0, 1.0)) * 0.3183098733425140380859375) * (Standard3DLightBlock.EnvironmentSize.y - 1.0);
        highp vec4 _993 = texture(u_fragment_tex7, vec2(_961, (((_970 * _975) + 1.5) + _979) / Standard3DLightBlock.EnvironmentSize.z));
        highp vec4 _996 = texture(u_fragment_tex7, vec2(_961, ((((isnan(_968) ? _971 : (isnan(_971) ? _968 : min(_971, _968))) * _975) + 1.5) + _979) / Standard3DLightBlock.EnvironmentSize.z));
        highp vec4 _1005 = texture(u_fragment_tex8, vec2(_660, _291));
        highp vec3 _1018 = normalize(vec3((_945 * _271.x) - (_944 * _271.z), _271.y, (_944 * _271.x) + (_945 * _271.z)));
        highp vec4 _1030 = texture(u_fragment_tex6, vec2((atan(_1018.z, _1018.x) * 0.15915493667125701904296875) + 0.5, acos(clamp(_1018.y, -1.0, 1.0)) * 0.3183098733425140380859375));
        highp vec3 _1068;
        if (_895)
        {
            _1068 = _227.xyz;
        }
        else
        {
            highp float _1046;
            if (_227.x <= 0.040449999272823333740234375)
            {
                _1046 = _227.x * 0.077399380505084991455078125;
            }
            else
            {
                _1046 = pow((_227.x + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _1056;
            if (_227.y <= 0.040449999272823333740234375)
            {
                _1056 = _227.y * 0.077399380505084991455078125;
            }
            else
            {
                _1056 = pow((_227.y + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _1066;
            if (_227.z <= 0.040449999272823333740234375)
            {
                _1066 = _227.z * 0.077399380505084991455078125;
            }
            else
            {
                _1066 = pow((_227.z + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _1068 = vec3(_1046, _1056, _1066);
        }
        highp vec3 _1081 = ((((((vec3(1.0) - (_931 + ((vec3(_1342.x ? _933.x : _1344.x, _1342.y ? _933.y : _1344.y, _1342.z ? _933.z : _1344.z) - _931) * pow(1.0 - _660, 5.0)))) * _700) * _1068) * _1030.xyz) + (mix(_993.xyz, _996.xyz, vec3(_969 - _970)) * ((_931 * _1005.x) + vec3(_1005.y)))) * Standard3DLightBlock.Environment.y) * _881;
        highp vec3 _1116;
        if (_895)
        {
            _1116 = _1081;
        }
        else
        {
            highp float _1085 = _1081.x;
            highp float _1094;
            if (_1085 <= 0.003130800090730190277099609375)
            {
                _1094 = _1085 * 12.9200000762939453125;
            }
            else
            {
                _1094 = (1.05499994754791259765625 * pow(_1085, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _1095 = _1081.y;
            highp float _1104;
            if (_1095 <= 0.003130800090730190277099609375)
            {
                _1104 = _1095 * 12.9200000762939453125;
            }
            else
            {
                _1104 = (1.05499994754791259765625 * pow(_1095, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _1105 = _1081.z;
            highp float _1114;
            if (_1105 <= 0.003130800090730190277099609375)
            {
                _1114 = _1105 * 12.9200000762939453125;
            }
            else
            {
                _1114 = (1.05499994754791259765625 * pow(_1105, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            _1116 = vec3(_1094, _1104, _1114);
        }
        _1118 = _887 + _1116;
    }
    else
    {
        _1118 = _887;
    }
    highp vec3 _1217;
    if (Standard3DMaterialBlock.TextureFlags.z > 0.5)
    {
        highp vec4 _1130 = texture(u_fragment_tex5, v_loc2);
        bool _1140;
        if (Standard3DLightBlock.ColorPipeline.x > 0.5)
        {
            _1140 = Standard3DMaterialBlock.TextureFlags.w < 0.5;
        }
        else
        {
            _1140 = false;
        }
        highp vec3 _1174;
        if (_1140)
        {
            highp float _1143 = _1130.x;
            highp float _1152;
            if (_1143 <= 0.040449999272823333740234375)
            {
                _1152 = _1143 * 0.077399380505084991455078125;
            }
            else
            {
                _1152 = pow((_1143 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _1153 = _1130.y;
            highp float _1162;
            if (_1153 <= 0.040449999272823333740234375)
            {
                _1162 = _1153 * 0.077399380505084991455078125;
            }
            else
            {
                _1162 = pow((_1153 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            highp float _1163 = _1130.z;
            highp float _1172;
            if (_1163 <= 0.040449999272823333740234375)
            {
                _1172 = _1163 * 0.077399380505084991455078125;
            }
            else
            {
                _1172 = pow((_1163 + 0.054999999701976776123046875) * 0.947867333889007568359375, 2.400000095367431640625);
            }
            _1174 = vec3(_1152, _1162, _1172);
        }
        else
        {
            _1174 = _1130.xyz;
        }
        bool _1181;
        if (Standard3DLightBlock.ColorPipeline.x < 0.5)
        {
            _1181 = Standard3DMaterialBlock.TextureFlags.w > 0.5;
        }
        else
        {
            _1181 = false;
        }
        highp vec3 _1215;
        if (_1181)
        {
            highp float _1193;
            if (_1174.x <= 0.003130800090730190277099609375)
            {
                _1193 = _1174.x * 12.9200000762939453125;
            }
            else
            {
                _1193 = (1.05499994754791259765625 * pow(_1174.x, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _1203;
            if (_1174.y <= 0.003130800090730190277099609375)
            {
                _1203 = _1174.y * 12.9200000762939453125;
            }
            else
            {
                _1203 = (1.05499994754791259765625 * pow(_1174.y, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            highp float _1213;
            if (_1174.z <= 0.003130800090730190277099609375)
            {
                _1213 = _1174.z * 12.9200000762939453125;
            }
            else
            {
                _1213 = (1.05499994754791259765625 * pow(_1174.z, 0.4166666567325592041015625)) - 0.054999999701976776123046875;
            }
            _1215 = vec3(_1193, _1203, _1213);
        }
        else
        {
            _1215 = _1174;
        }
        _1217 = Standard3DMaterialBlock.Emissive.xyz * _1215;
    }
    else
    {
        _1217 = Standard3DMaterialBlock.Emissive.xyz;
    }
    highp vec3 _1218 = _1118 + _1217;
    bool _1227;
    if (Standard3DShadowBlock.CascadeSettings.z > 0.5)
    {
        _1227 = Standard3DShadowBlock.CascadeSettings.x > 0.5;
    }
    else
    {
        _1227 = false;
    }
    highp vec3 _1268;
    if (_1227)
    {
        highp float _1234 = dot(v_loc4 - Standard3DLightBlock.CameraPosition.xyz, Standard3DShadowBlock.ShadowCameraForward.xyz);
        int _1252;
        if (_1234 <= Standard3DShadowBlock.CascadeSplits.x)
        {
            _1252 = 0;
        }
        else
        {
            int _1251;
            if (_1234 <= Standard3DShadowBlock.CascadeSplits.y)
            {
                _1251 = 1;
            }
            else
            {
                _1251 = (_1234 <= Standard3DShadowBlock.CascadeSplits.z) ? 2 : 3;
            }
            _1252 = _1251;
        }
        int _1253 = (_1252);
        highp vec3 _1266;
        if (_1253 == 0)
        {
            _1266 = vec3(1.0, 0.20000000298023223876953125, 0.20000000298023223876953125);
        }
        else
        {
            highp vec3 _1265;
            if (_1253 == 1)
            {
                _1265 = vec3(0.20000000298023223876953125, 1.0, 0.20000000298023223876953125);
            }
            else
            {
                bvec3 _1263 = bvec3(_1253 == 2);
                _1265 = vec3(_1263.x ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).x : vec3(1.0, 1.0, 0.20000000298023223876953125).x, _1263.y ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).y : vec3(1.0, 1.0, 0.20000000298023223876953125).y, _1263.z ? vec3(0.20000000298023223876953125, 0.20000000298023223876953125, 1.0).z : vec3(1.0, 1.0, 0.20000000298023223876953125).z);
            }
            _1266 = _1265;
        }
        _1268 = mix(_1218, _1266, vec3(0.4000000059604644775390625));
    }
    else
    {
        _1268 = _1218;
    }
    highp vec3 _1276;
    if (Standard3DLightBlock.ColorPipeline.x > 0.5)
    {
        _1276 = _1268;
    }
    else
    {
        _1276 = clamp(_1268, vec3(0.0), vec3(1.0));
    }
    out_var_SV_Target0 = vec4(_1276, _227.w);
}
