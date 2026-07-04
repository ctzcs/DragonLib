using System;
using System.IO;
using System.Text.Json;
using Foster.Framework;

namespace Engine.Assets;

/// <summary>
/// 资源扫描器（README 里规划的那一步）：遍历一个内容目录，把里面的文件反序列化成
/// <b>调用方指定</b>的资产类型 <typeparamref name="T"/>，再用「相对内容根的路径」作 name
/// 登记进 <see cref="AssetDatabase"/>。
///
/// 类型由调用方决定（<c>ScanInto&lt;Prefab&gt;("Resources/Prefabs")</c>），不靠扩展名去猜——
/// 因为 .json 可能是 Prefab / 关卡 / 配置表，扩展名分不清；目录才是分类依据。
///
/// 名字权威：name = 相对 <c>nameRoot</c> 的路径。<see cref="AssetId.Normalize"/> 会剥掉扩展名，
/// 所以 <c>Prefabs/enemy.json</c> 与 <c>Prefabs/enemy</c> 得到同一个 <see cref="AssetId"/>，
/// 与创作端（编辑器 Save）用同一路径算出的 id 一致。
/// </summary>
public static class ContentScanner
{
    /// <summary>
    /// 扫描 <paramref name="scanDir"/>（相对 storage 根，递归）下所有文件，逐个按 JSON 反序列化成
    /// <typeparamref name="T"/>，用相对 <paramref name="nameRoot"/> 的路径作 name 登记进 <paramref name="assets"/>。
    /// <paramref name="nameRoot"/> 为空时相对 <paramref name="scanDir"/>。返回成功登记的数量。
    /// </summary>
    public static int ScanInto<T>(
        AssetDatabase assets,
        StorageContainer storage,
        string scanDir,
        string? nameRoot = null,
        JsonSerializerOptions? options = null) where T : class
    {
        if (!storage.DirectoryExists(scanDir)) return 0;
        nameRoot ??= scanDir;

        int count = 0;
        foreach (var path in storage.EnumerateDirectory(scanDir, "*", SearchOption.AllDirectories))
        {
            try
            {
                var text = storage.ReadAllText(path);
                var asset = JsonSerializer.Deserialize<T>(text, options);
                if (asset == null)
                {
                    Log.Warning($"ContentScanner: '{path}' 反序列化为 null，已跳过。");
                    continue;
                }

                assets.Register(RelativeName(path, nameRoot), asset);
                count++;
            }
            catch (Exception ex)
            {
                Log.Warning($"ContentScanner: 加载 '{path}' 失败，已跳过。{ex.Message}");
            }
        }
        return count;
    }

    /// <summary>把「相对 storage 根」的路径转成「相对 nameRoot」的路径（扩展名保留，交给 Normalize 去剥）。</summary>
    private static string RelativeName(string path, string nameRoot)
    {
        var p = path.Replace('\\', '/');
        var root = nameRoot.Replace('\\', '/').Trim('/');
        if (root.Length > 0 && p.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            p = p[(root.Length + 1)..];
        return p;
    }
}
