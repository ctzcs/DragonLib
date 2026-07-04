using System.Collections.Generic;
using System.Text.Json;
using DCFApixels.DragonECS;
using Engine.Assets;

namespace Engine.ECS;

/// <summary>
/// 让一个实体带上"内容身份"：它是从哪个预制体实例化来的。
/// 编辑器 / 序列化据此知道该导出成哪种对象、以及相对预制体默认值的差异。
/// </summary>
public struct PrefabRefComp : IEcsComponent
{
    public AssetId Prefab;
}

/// <summary>
/// 让一个实体带上"实例身份"（<see cref="SpawnId"/>）。用于关卡内交叉引用。
/// 见 Assets/README.md 的 SpawnId 一节。
/// </summary>
public struct SpawnIdComp : IEcsComponent
{
    public SpawnId Id;
}

/// <summary>
/// 预制体定义：一个可复用的实体模板，对应 Noel 关卡 JSON 里的 "type": "Elevator"。
/// 本身是一份内容资产（有 <see cref="AssetId"/>，进 AssetDatabase）。
///
/// Components 是"组件类型名 → 默认值(JSON)"。实例化时先套用这些默认组件，
/// 再套用关卡记录里的字段覆盖（override）。这样关卡文件只需存与默认不同的部分。
/// </summary>
public sealed class Prefab
{
    public AssetId Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>组件类型名（Type.Name）→ 默认值。用 JsonElement 保存，实例化时按类型反序列化。</summary>
    public Dictionary<string, JsonElement> Components { get; set; } = new();
}
