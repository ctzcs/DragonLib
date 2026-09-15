using System;
using System.IO;
using Engine;
using Engine.Assets;
using Foster.Framework;

namespace DragonLib.Gltf;

/// <summary>
/// 扫描模型目录（.glb/.gltf，含 FBX 经 Tools\FbxToGltf 转出的产物），加载后按
/// 「相对 nameRoot 的路径」作 name 登记进 <see cref="AssetDatabase"/>。
/// 命名约定与 <see cref="ContentScanner"/> 一致（扩展名由 AssetId.Normalize 剥掉）。
/// </summary>
public static class GltfModelScanner
{
    public static int ScanInto(
        AssetDatabase assets,
        GraphicsDevice device,
        LocalStorage storage,
        string scanDir,
        string? nameRoot = null)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(storage);
        if (!storage.DirectoryExists(scanDir)) return 0;
        nameRoot ??= scanDir;

        var count = 0;
        foreach (var path in storage.EnumerateDirectory(scanDir, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".glb" && extension != ".gltf")
                continue;
            if (!storage.FileExists(path))
                continue;

            try
            {
                var model = GltfModelLoader.Load(device, storage, path);
                assets.Register(RelativeName(path, nameRoot), model);
                count++;
            }
            catch (Exception ex)
            {
                Log.Warning($"GltfModelScanner: 加载 '{path}' 失败，已跳过。{ex.Message}");
            }
        }
        return count;
    }

    /// <summary>把「相对 storage 根」的路径转成「相对 nameRoot」的路径（与 ContentScanner 相同规则）。</summary>
    private static string RelativeName(string path, string nameRoot)
    {
        var p = path.Replace('\\', '/');
        var root = nameRoot.Replace('\\', '/').Trim('/');
        if (root.Length > 0 && p.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            p = p[(root.Length + 1)..];
        return p;
    }
}
