using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine.Assets;

/// <summary>
/// 内容资产的稳定 id：一份被创作的内容（sprite、tileset、prefab……）的身份。
/// 权威来源是资源的相对路径名，id 由路径经 FNV-1a 64bit 哈希得到（<see cref="FromName"/>）。
/// 因此不需要中央 id 表 / sidecar：运行期加载资源时凭路径重建 hash→asset 映射即可。
/// 代价：重命名 / 移动资源会改变 id，旧引用会失效。
///
/// 与 SpawnId 区分：AssetId 指向"内容"，SpawnId 指向"关卡里被摆放的这一个实例"。
/// 序列化为裸 long。
/// </summary>
[JsonConverter(typeof(AssetIdConverter))]
public readonly record struct AssetId(long Value)
{
    public static readonly AssetId None = default;
    public bool IsValid => Value != 0;

    /// <summary>由 <see cref="AssetIdGenerator"/> 发号（仅在需要唯一而非按名寻址时用，一般走 <see cref="FromName"/>）。</summary>
    public static AssetId New() => new(AssetIdGenerator.Next());

    /// <summary>
    /// 由资源相对路径生成稳定 id。名字会先规范化（去扩展名、小写、统一正斜杠），
    /// 保证跨平台 / 跨运行一致。例如 "objects/lift_outskirts"。
    /// </summary>
    public static AssetId FromName(string name) => new(Fnv1a64(Normalize(name)));

    /// <summary>把任意路径规范成用于哈希的 key：正斜杠、去首尾斜杠、去扩展名、小写。</summary>
    public static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var s = name.Replace('\\', '/').Trim('/');
        int dot = s.LastIndexOf('.');
        int slash = s.LastIndexOf('/');
        if (dot > slash) s = s[..dot]; // 去扩展名（仅当点在最后一段里）
        return s.ToLowerInvariant();
    }

    /// <summary>FNV-1a 64bit。无依赖、稳定、跨平台一致。结果转成 long（保留全部 64bit）。</summary>
    /// 相同字符串永远生成相同id
    private static long Fnv1a64(string s)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        // 按 UTF-8 字节哈希，保证与语言 / 编码无关
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= prime;
        }
        long value = unchecked((long)hash);
        // 0 保留给 None：极小概率命中时偏移一位
        return value == 0 ? 1 : value;
    }

    public override string ToString() => Value.ToString();

    /// <summary>调试用 16 进制表示（编辑器 UI 里常这么显示 id）。</summary>
    public string ToHex() => Value.ToString("X16");
}

public sealed class AssetIdConverter : JsonConverter<AssetId>
{
    public override AssetId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetInt64());
    public override void Write(Utf8JsonWriter w, AssetId v, JsonSerializerOptions o) => w.WriteNumberValue(v.Value);
}
