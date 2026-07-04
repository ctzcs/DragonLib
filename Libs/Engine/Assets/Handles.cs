using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine.Assets;

/// <summary>
/// 关卡里"被摆放的一个实例"的身份。用于交叉引用（例如一扇门引用它开的开关）。
/// 与 <see cref="AssetId"/> 区分：AssetId 指向"被创作的内容"，SpawnId 指向"关卡里的这一个"。
/// 由 <see cref="AssetIdGenerator"/> 发号（不能按名字哈希：同一预制体摆多次，名字相同但需要不同 id）。
/// 存在关卡文件里，随实例一起序列化。序列化为裸 long。
/// </summary>
[JsonConverter(typeof(SpawnIdConverter))]
public readonly record struct SpawnId(long Value)
{
    public static readonly SpawnId None = default;
    public bool IsValid => Value != 0;
    public static SpawnId New() => new(AssetIdGenerator.Next());
    public override string ToString() => Value.ToString();

    /// <summary>编辑器 UI 里常用的 16 进制表示（如 "51437F46705D359C"）。</summary>
    public string ToHex() => Value.ToString("X16");
}

/// <summary>
/// 指向一份内容资产的可序列化句柄。序列化为裸 long，运行时经 <see cref="AssetRegistry"/> 解析成真对象。
/// 字段里写 <c>AssetRef&lt;Sprite&gt;</c>，磁盘上就是 <c>"spr": 15634960586538533060</c>。
/// </summary>
[JsonConverter(typeof(AssetRefConverterFactory))]
public readonly record struct AssetRef<T>(AssetId Id) where T : class
{
    public bool IsValid => Id.IsValid;
    public T? Resolve(AssetRegistry reg) => reg.TryResolve(Id, out var o) ? o as T : null;
    public static implicit operator AssetRef<T>(AssetId id) => new(id);
    public override string ToString() => Id.ToString();
}

/// <summary>
/// 指向"同一关卡里另一个实例"的可序列化句柄。序列化为裸 long(SpawnId)，
/// 加载第二趟时解析成真实实体。与 <see cref="AssetRef{T}"/> 对称。
/// </summary>
[JsonConverter(typeof(EntityRefConverter))]
public readonly record struct EntityRef(SpawnId Target)
{
    public static readonly EntityRef None = default;
    public bool IsValid => Target.IsValid;
    public override string ToString() => Target.ToString();
}

public sealed class SpawnIdConverter : JsonConverter<SpawnId>
{
    public override SpawnId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetInt64());
    public override void Write(Utf8JsonWriter w, SpawnId v, JsonSerializerOptions o) => w.WriteNumberValue(v.Value);
}

public sealed class EntityRefConverter : JsonConverter<EntityRef>
{
    public override EntityRef Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(new SpawnId(r.GetInt64()));
    public override void Write(Utf8JsonWriter w, EntityRef v, JsonSerializerOptions o) => w.WriteNumberValue(v.Target.Value);
}

/// <summary>AssetRef&lt;T&gt; 是泛型，需要 factory 按具体 T 生成 converter。</summary>
public sealed class AssetRefConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(AssetRef<>);

    public override JsonConverter CreateConverter(Type t, JsonSerializerOptions o)
    {
        var inner = t.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(
            typeof(AssetRefConverter<>).MakeGenericType(inner))!;
    }
}

public sealed class AssetRefConverter<T> : JsonConverter<AssetRef<T>> where T : class
{
    public override AssetRef<T> Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(new AssetId(r.GetInt64()));
    public override void Write(Utf8JsonWriter w, AssetRef<T> v, JsonSerializerOptions o) => w.WriteNumberValue(v.Id.Value);
}
