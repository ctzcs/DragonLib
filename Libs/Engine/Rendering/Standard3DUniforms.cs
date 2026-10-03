using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine.Rendering;

/// <summary>Standard3D fragment b0, space3。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct LightUniforms
{
    public Vector4 LightDirection;
    public Vector4 Ambient;
    public Vector4 Diffuse;
    public Vector4 CameraPosition;
    public Vector4 ColorPipeline; // x: HDR 线性光照；0 保留 LDR gamma 光照观感。
}

/// <summary>Standard3D fragment b1, space3。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct MaterialUniforms
{
    public Vector4 BaseColorFactor;
    public Vector4 Flags; // albedo、normal、normal strength、alpha mode
    public Vector4 AlphaParams;
    public Vector4 PbrParams;
}

/// <summary>Standard3D vertex b1, space1。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ShadowMatrixUniforms
{
    public Matrix4x4 LightViewProjection;
}

/// <summary>Standard3D fragment b2, space3。</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ShadowSettingsUniforms
{
    public Vector4 Settings; // enabled、texel size、bias、darkness
}
