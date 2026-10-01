namespace Engine.ECS;

/// <summary>
/// ECS 组件的分层约定。写组件前先想清楚它属于哪一层：
///
/// ① <b>配置层（创作数据）</b>：设计者 / 关卡文件说了算的值，随 prefab/level 序列化。
///    例：<c>Transform3DComp</c>、<c>Parent3DComp</c>、<c>MeshRendererComp</c>、2D 的 <c>PositionComp</c>。
///
/// ② <b>身份层</b>：实例化时由 <see cref="PrefabSerializer"/> 打上、由 LevelSerializer 以
///    EntityData.Id / Def 字段落盘的组件，存盘路径不需要（也 shouldn't）看到它们。
///    例：<c>SpawnIdComp</c>、<c>PrefabRefComp</c>。
///
/// ③ <b>派生层（Runtime）</b>：由系统每帧从 ① 重算出来的缓存，实现本接口标记。
///    存盘只会制造噪声，序列化统一经 <see cref="EcsSerialization.IsExcluded"/> 跳过。
///    例：<c>LocalToWorldComp</c>（Transform3DSystem 写入）。
///
/// 判别方法：改了它之后游戏存档/关卡文件里"应不应该出现这个值"——应该出现 → ①；
/// 由加载器重建 → ②；由 Update 重算 → ③。
/// </summary>
public interface IEcsDerivedComponent
{
}
