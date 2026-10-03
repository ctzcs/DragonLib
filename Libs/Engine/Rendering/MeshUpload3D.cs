using System.Buffers;
using Foster.Framework;

namespace Engine.Rendering;

/// <summary>把 Engine 的 CCW 三角形索引上传为 Foster 后端采用的绕序。</summary>
public static class MeshUpload3D
{
    /// <summary>
    /// 输入为从外侧看 CCW 的三角形列表，使用 32 位索引。
    /// 桌面 SDL_GPU 沿用 Foster 的 CW 正面设置，上传时交换每个三角形的后两个索引；
    /// WebGL 的正面设置已配合离屏 shader 的 y 翻转，保留 CCW 输入。
    /// 不修改源数组，CPU 拾取数据和 .dasset 文件仍采用 CCW。
    /// 自建 Foster Mesh 后交给 Renderer3D 时，也应通过此入口上传三角形索引。
    /// </summary>
    public static unsafe void SetTriangleIndices(Mesh mesh, ReadOnlySpan<uint> ccwIndices)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.IndexFormat != IndexFormat.ThirtyTwo)
            throw new ArgumentException("3D triangle uploads require 32-bit indices.", nameof(mesh));
        if (ccwIndices.Length % 3 != 0)
            throw new ArgumentException("Triangle indices must contain complete triples.", nameof(ccwIndices));

        if (OperatingSystem.IsBrowser() || ccwIndices.IsEmpty)
        {
            fixed (uint* data = ccwIndices)
                mesh.SetIndices((nint)data, ccwIndices.Length);
            return;
        }

        var scratch = ArrayPool<uint>.Shared.Rent(ccwIndices.Length);
        try
        {
            for (var i = 0; i < ccwIndices.Length; i += 3)
            {
                scratch[i] = ccwIndices[i];
                scratch[i + 1] = ccwIndices[i + 2];
                scratch[i + 2] = ccwIndices[i + 1];
            }
            fixed (uint* data = scratch)
                mesh.SetIndices((nint)data, ccwIndices.Length);
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(scratch);
        }
    }
}
