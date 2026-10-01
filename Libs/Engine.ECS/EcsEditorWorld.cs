using System.Diagnostics;
using DCFApixels.DragonECS;

namespace Engine.ECS;

/// <summary>
/// 编辑器专用世界。和 <see cref="EcsDefaultWorld"/> 一样，只是 <see cref="EcsWorld"/> 的空壳子类，
/// 目的是给 DI（AutoInject）一个可区分的类型，用来存放编辑器态的实体（预览、gizmo、临时选择等），
/// 不与运行时的 EcsDefaultWorld 混在一起。
/// </summary>
[MetaColor(MetaColor.DragonCyan)]
[MetaGroup(EcsConsts.PACK_GROUP, EcsConsts.WORLDS_GROUP)]
[MetaDescription(EcsConsts.AUTHOR, "Inherits EcsWorld without extending its functionality. Used to store editor-side entities separately from the runtime world.")]
[DebuggerTypeProxy(typeof(DebuggerProxy))]
[MetaID("Engine_5A1C7F0E4B2D3A6C9E8F1D2B3C4E5F60")]
public sealed class EcsEditorWorld : EcsWorld, IInjectionUnit
{
    private const string DEFAULT_NAME = "Editor";
    public EcsEditorWorld() : base(default(EcsWorldConfig), DEFAULT_NAME) { }
    public EcsEditorWorld(EcsWorldConfig config = null, string name = null, short worldID = -1) : base(config, name == null ? DEFAULT_NAME : name, worldID) { }
    public EcsEditorWorld(IConfigContainer configs, string name = null, short worldID = -1) : base(configs, name == null ? DEFAULT_NAME : name, worldID) { }
    void IInjectionUnit.InitInjectionNode(InjectionGraph nodes) { nodes.AddNode(this); }
}
