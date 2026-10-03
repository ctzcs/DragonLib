using Engine.DearImGui;
using ImGuiNET;

namespace Engine.Assets;

/// <summary>
/// AssetRef&lt;T&gt; 的属性绘制器：对应截图里 Elevator 面板的 "Spr" 那一行——
/// 一个下拉，列出该类型所有已加载资产的名字，选中后内部存对应的哈希 id。
///
/// 绘制器只拿得到哈希 id，要显示 "objects/lift_outskirts" 必须反查名字，
/// 因此依赖 <see cref="AssetDatabase"/> 的反向表。数据库通过 <see cref="Bind"/> 注入。
/// </summary>
public sealed class AssetRefDrawer<T> : PropertyDrawer<AssetRef<T>> where T : class
{
    // 编辑器全局唯一的资产库。由宿主在初始化 inspector 时 Bind。
    private static AssetDatabase? _db;
    public static void Bind(AssetDatabase db) => _db = db;

    protected override bool OnDraw(string label, ref AssetRef<T> value)
    {
        if (_db == null)
        {
            ImGui.LabelText(label, "(no AssetDatabase bound)");
            return false;
        }

        var names = _db.NamesOf<T>();
        string current = value.IsValid
            ? (_db.GetName(value.Id) ?? $"<missing {value.Id.ToHex()}>")
            : "(none)";

        bool changed = false;
        if (ImGui.BeginCombo(label, current))
        {
            // "(none)" 选项，用于清空引用
            if (ImGui.Selectable("(none)", !value.IsValid))
            {
                value = new AssetRef<T>(AssetId.None);
                changed = true;
            }

            for (int i = 0; i < names.Count; i++)
            {
                var name = names[i];
                var id = AssetId.FromName(name);
                bool selected = value.Id == id;
                if (ImGui.Selectable(name, selected))
                {
                    value = new AssetRef<T>(id);
                    changed = true;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        return changed;
    }
}
