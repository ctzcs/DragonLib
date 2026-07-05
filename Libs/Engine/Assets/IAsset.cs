namespace Engine.Assets;

/// <summary>
/// 一份内容资产的公共契约。Prefab、（未来的）Sprite / Tileset 等都实现它。
///
/// <para><b>Id 的语义</b>：id 不是资产自己"想出来"的，而是它在 <see cref="AssetDatabase"/> 里
/// 登记时由路径名算出（<see cref="AssetId.FromName"/>），并在 <see cref="AssetDatabase.Register"/>
/// 时<b>回填</b>进这个字段。因此磁盘上存的 Id 永远等于注册 id，单一真相源。</para>
///
/// <para>这个字段主要供<b>存盘追溯</b>：关卡 json 里看到裸的 <c>Def: 123…</c>，可 grep 各资产
/// json 的 Id 找回文件、再看 <see cref="Name"/> 得知人类可读名。运行时解析引用走
/// <see cref="AssetDatabase"/> 的 id→对象 表，不依赖资产读自己的 Id。</para>
/// </summary>
public interface IAsset
{
    /// <summary>登记 id（由 <see cref="AssetDatabase.Register"/> 按名字算出并回填）。</summary>
    AssetId Id { get; set; }

    /// <summary>人类可读名（调试 / 编辑器显示用）。</summary>
    string Name { get; set; }
}
