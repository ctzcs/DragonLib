#version 300 es
precision highp float;
precision highp int;

layout(std140) uniform FragmentUniform0
{
    highp vec4 LightDirection;
    highp vec4 Ambient;
    highp vec4 Diffuse;
    highp vec4 CameraPosition;
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
    highp vec4 _96;
    if (Standard3DMaterialBlock.MaterialFlags.x > 0.5)
    {
        _96 = Standard3DMaterialBlock.BaseColorFactor * texture(u_fragment_tex0, v_loc2);
    }
    else
    {
        _96 = Standard3DMaterialBlock.BaseColorFactor;
    }
    bool _103;
    if (Standard3DMaterialBlock.MaterialFlags.w > 0.5)
    {
        _103 = Standard3DMaterialBlock.MaterialFlags.w < 1.5;
    }
    else
    {
        _103 = false;
    }
    if (_103)
    {
        if ((_96.w - Standard3DMaterialBlock.AlphaParams.x) < 0.0)
        {
            discard;
        }
    }
    highp vec3 _113 = normalize(v_loc0);
    highp vec3 _140;
    if (Standard3DMaterialBlock.MaterialFlags.y > 0.5)
    {
        highp vec3 _120 = normalize(v_loc1.xyz);
        highp vec3 _132 = (texture(u_fragment_tex1, v_loc2).xyz * 2.0) - vec3(1.0);
        highp vec2 _136 = _132.xy * Standard3DMaterialBlock.MaterialFlags.z;
        _140 = normalize(mat3(_120, cross(_113, _120) * v_loc1.w, _113) * vec3(_136.x, _136.y, _132.z));
    }
    else
    {
        _140 = _113;
    }
    highp float _144 = clamp(Standard3DMaterialBlock.PbrParams.x, 0.0, 1.0);
    highp float _147 = clamp(Standard3DMaterialBlock.PbrParams.y, 0.0500000007450580596923828125, 1.0);
    highp vec3 _152 = normalize(Standard3DLightBlock.CameraPosition.xyz - v_loc4);
    highp float _229;
    do
    {
        if (Standard3DShadowBlock.ShadowSettings.x < 0.5)
        {
            _229 = 1.0;
            break;
        }
        highp vec3 _164 = v_loc3.xyz / vec3(v_loc3.w);
        highp vec2 _167 = (_164.xy * 0.5) + vec2(0.5);
        highp float _168 = _167.x;
        bool _174;
        if (!(_168 < 0.0))
        {
            _174 = _168 > 1.0;
        }
        else
        {
            _174 = true;
        }
        bool _180;
        if (!_174)
        {
            _180 = _167.y < 0.0;
        }
        else
        {
            _180 = true;
        }
        bool _186;
        if (!_180)
        {
            _186 = _167.y > 1.0;
        }
        else
        {
            _186 = true;
        }
        if (_186)
        {
            _229 = 1.0;
            break;
        }
        highp float _196;
        int _199;
        _196 = 0.0;
        _199 = -1;
        highp float _197;
        for (; _199 <= 1; _196 = _197, _199++)
        {
            _197 = _196;
            for (int _207 = -1; _207 <= 1; )
            {
                _197 += float((_164.z - Standard3DShadowBlock.ShadowSettings.z) <= texture(u_fragment_tex2, _167 + (vec2(float(_207), float(_199)) * Standard3DShadowBlock.ShadowSettings.y)).x);
                _207++;
                continue;
            }
        }
        _229 = 1.0 - (Standard3DShadowBlock.ShadowSettings.w * (1.0 - (_196 * 0.111111111938953399658203125)));
        break;
    } while(false);
    highp vec3 _234 = normalize(-Standard3DLightBlock.LightDirection.xyz);
    highp vec3 _240 = normalize(_152 + _234);
    highp float _242 = clamp(dot(_140, _234), 0.0, 1.0);
    highp float _244 = clamp(dot(_140, _152), 0.0, 1.0);
    highp vec3 _246 = mix(vec3(0.039999999105930328369140625), _96.xyz, vec3(_144));
    highp vec3 _249 = vec3(1.0) - _246;
    highp vec3 _253 = _246 + (_249 * pow(1.0 - clamp(dot(_240, _152), 0.0, 1.0), 5.0));
    highp float _254 = _147 * _147;
    highp float _255 = _254 * _254;
    highp float _257 = clamp(dot(_140, _240), 0.0, 1.0);
    highp float _259 = _255 - 1.0;
    highp float _261 = ((_257 * _257) * _259) + 1.0;
    highp float _265 = _147 + 1.0;
    highp float _267 = (_265 * _265) * 0.125;
    highp float _268 = 1.0 - _267;
    highp float _271 = _244 / ((_244 * _268) + _267);
    highp float _278 = 4.0 * _244;
    highp float _279 = _278 * _242;
    highp float _284 = 1.0 - _144;
    int _293 = min(int(Standard3DPointLightBlock.PointLightMeta.x), 16);
    highp vec3 _295;
    _295 = (((((vec3(1.0) - _253) * _284) * _96.xyz) + ((_253 * ((_255 / ((3.1415927410125732421875 * _261) * _261)) * (_271 * (_242 / ((_242 * _268) + _267))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _279 : (isnan(_279) ? 9.9999997473787516355514526367188e-05 : max(_279, 9.9999997473787516355514526367188e-05))))) * (Standard3DLightBlock.Diffuse.xyz * _229)) * _242;
    for (int _298 = 0; _298 < _293; )
    {
        highp vec3 _305 = Standard3DPointLightBlock.PointLightPositionRange[_298].xyz - v_loc4;
        highp float _306 = length(_305);
        highp float _312 = clamp(1.0 - (_306 / (isnan(0.001000000047497451305389404296875) ? Standard3DPointLightBlock.PointLightPositionRange[_298].w : (isnan(Standard3DPointLightBlock.PointLightPositionRange[_298].w) ? 0.001000000047497451305389404296875 : max(Standard3DPointLightBlock.PointLightPositionRange[_298].w, 0.001000000047497451305389404296875)))), 0.0, 1.0);
        highp vec3 _323 = _305 / vec3(isnan(9.9999997473787516355514526367188e-05) ? _306 : (isnan(_306) ? 9.9999997473787516355514526367188e-05 : max(_306, 9.9999997473787516355514526367188e-05)));
        highp vec3 _325 = normalize(_152 + _323);
        highp float _327 = clamp(dot(_140, _323), 0.0, 1.0);
        highp vec3 _333 = _246 + (_249 * pow(1.0 - clamp(dot(_325, _152), 0.0, 1.0), 5.0));
        highp float _335 = clamp(dot(_140, _325), 0.0, 1.0);
        highp float _338 = ((_335 * _335) * _259) + 1.0;
        highp float _348 = _278 * _327;
        _295 += ((((((vec3(1.0) - _333) * _284) * _96.xyz) + ((_333 * ((_255 / ((3.1415927410125732421875 * _338) * _338)) * (_271 * (_327 / ((_327 * _268) + _267))))) / vec3(isnan(9.9999997473787516355514526367188e-05) ? _348 : (isnan(_348) ? 9.9999997473787516355514526367188e-05 : max(_348, 9.9999997473787516355514526367188e-05))))) * ((Standard3DPointLightBlock.PointLightColorIntensity[_298].xyz * Standard3DPointLightBlock.PointLightColorIntensity[_298].w) * (_312 * _312))) * _327);
        _298++;
        continue;
    }
    out_var_SV_Target0 = vec4(clamp(_295 + (Standard3DLightBlock.Ambient.xyz * _96.xyz), vec3(0.0), vec3(1.0)), _96.w);
}
