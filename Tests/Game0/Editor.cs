using System;
using System.Collections.Generic;
using System.Numerics;
using DCFApixels.DragonECS;
using Engine;
using Engine.Assets;
using Engine.DearImGui;
using Engine.ECS;
using ImGuiNET;

namespace Game0;


public class EditorModule : IEcsModule
{
    public void Import(EcsPipeline.Builder b)
    {
        b.Add(new LevelEditorSystem());
    }
}
/// <summary>
/// 简易关卡编辑器（ImGui）：新建 / 保存 / 加载关卡，从预制体生成实体，编辑组件字段，
/// 把选中实体反存成预制体。复用 Engine 侧的 LevelSerializer / PrefabSerializer / AssetDatabase。
///
/// 依赖：构造时传入 AssetDatabase（预制体来源）；world 由 AutoInject 注入。
/// 用法（Program.cs）：.Add(new LevelEditorSystem(assets))
/// </summary>
public sealed class LevelEditorSystem : IUpdateSystem
{
    [DI] private EcsEditorWorld _world;
    [DI] private AssetDatabase _assets;

    public bool IsOpen = true;
    public string WindowTitle = "Level Editor";

    private string _levelName = "untitled";
    private string _newPrefabName = "new_prefab";
    private int _selectedEntity = -1;
    private string _status = "";
    private string _addComponentFilter = "";
    
    // 内容根：所有资产 name 都相对它（如 "Prefabs/enemy"），与启动扫描器的 nameRoot 一致，
    // 保证 Save 与 Scan 算出同一个 AssetId。
    private const string ContentRoot = "Resources";

    // 资产内容名（用于 AssetId / Register）：相对内容根、带子目录，不含扩展名。
    private static string PrefabName(string shortName) => "Prefabs/" + shortName;

    // 磁盘路径（相对 game root）= 内容根 / 内容名 .json。
    private static string LevelPath(string shortName) => ContentRoot + "/Levels/" + shortName + ".json";
    private static string PrefabPath(string shortName) => ContentRoot + "/" + PrefabName(shortName) + ".json";

    public void Update()
    {
        if (!IsOpen) return;
        if (!ImGui.Begin(WindowTitle, ref IsOpen)) { ImGui.End(); return; }

        DrawLevelToolbar();
        ImGui.Separator();
        DrawPrefabPalette();
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        float leftW = MathF.Max(180f, avail.X * 0.35f);
        ImGui.BeginChild("##lvl_list", new Vector2(leftW, 0), ImGuiChildFlags.Borders);
        DrawEntityList();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##lvl_detail", new Vector2(0, 0), ImGuiChildFlags.Borders);
        DrawEntityDetail();
        ImGui.EndChild();

        ImGui.End();
    }

    private void DrawLevelToolbar()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("Level", ref _levelName, 64);
        ImGui.SameLine();
        if (ImGui.Button("New")) { ClearLevel(); _status = "New empty level."; }
        ImGui.SameLine();
        if (ImGui.Button("Save")) SaveLevel();
        ImGui.SameLine();
        if (ImGui.Button("Load")) LoadLevel();
        ImGui.SameLine();
        if (ImGui.Button("+ Empty")) CreateEmptyEntity();
        if (!string.IsNullOrEmpty(_status)) ImGui.TextDisabled(_status);
    }

    /// <summary>创建一个空白实体，只打身份组件（SpawnId），选中它。之后可在右侧 Inspector 加组件、Save as Prefab。</summary>
    private void CreateEmptyEntity()
    {
        int e = _world.NewEntity();
        _world.GetPool<SpawnIdComp>().Add(e).Id = SpawnId.New();
        _selectedEntity = e;
        _status = $"Created empty entity #{e}.";
    }

    private void DrawPrefabPalette()
    {
        ImGui.Text("Prefabs (click to spawn):");
        var names = _assets.NamesOf<Prefab>();
        if (names.Count == 0)
        {
            ImGui.TextDisabled("(no prefabs registered — spawn nothing, or 'Save as Prefab' below)");
            return;
        }
        for (int i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (ImGui.Button($"+ {name}"))
            {
                var prefab = _assets.Get<Prefab>(name);
                if (prefab != null)
                {
                    _selectedEntity = PrefabSerializer.Instantiate(_world, prefab);
                    _status = $"Spawned '{name}'.";
                }
            }
            if (i % 3 != 2) ImGui.SameLine();
        }
        ImGui.NewLine();
    }

    private void DrawEntityList()
    {
        var spawnPool = _world.GetPool<SpawnIdComp>();
        var prefabPool = _world.GetPool<PrefabRefComp>();

        foreach (int e in _world.Entities)
        {
            if (!spawnPool.Has(e)) continue;   // 有实例身份即列出（空实体也算），prefab 只是可选标注
            var id = spawnPool.Get(e).Id;
            string tag = prefabPool.Has(e) ? "" : " (empty)";
            bool selected = _selectedEntity == e;
            if (ImGui.Selectable($"#{e}  {id.ToHex()}{tag}", selected))
                _selectedEntity = e;
        }
    }

    private void DrawEntityDetail()
    {
        if (_selectedEntity < 0 || !_world.IsUsed(_selectedEntity))
        {
            ImGui.TextDisabled("Select or spawn an entity");
            return;
        }

        int e = _selectedEntity;
        var spawnPool = _world.GetPool<SpawnIdComp>();
        var prefabPool = _world.GetPool<PrefabRefComp>();

        if (spawnPool.Has(e))
            ImGui.Text($"SpawnId: {spawnPool.Get(e).Id.ToHex()}");
        if (prefabPool.Has(e))
            ImGui.Text($"Prefab:  {_assets.GetName(prefabPool.Get(e).Prefab) ?? prefabPool.Get(e).Prefab.ToHex()}");

        ImGui.SameLine();
        if (ImGui.SmallButton("Delete")) { _world.DelEntity(e); _selectedEntity = -1; return; }

        ImGui.Separator();

        // 复用 Inspector 的反射绘制：逐组件画字段（跳过身份组件）。
        var pools = _world.AllPools;
        foreach (int cid in _world.GetComponentTypeIDsFor(e))
        {
            var pool = pools[cid];
            var type = pool.ComponentType;
            if (type == typeof(SpawnIdComp) || type == typeof(PrefabRefComp)) continue;

            ImGui.PushID(type.FullName ?? type.Name);
            if (ImGui.CollapsingHeader(type.Name, ImGuiTreeNodeFlags.DefaultOpen))
            {
                object? raw = pool.GetRaw(e);
                if (raw != null && ReflectionStructDrawer.DrawStructBody(ref raw, type))
                    pool.SetRaw(e, raw!);
            }
            ImGui.PopID();
        }

        ImGui.Separator();
        DrawAddComponent(e);
        ImGui.Separator();
        DrawSaveAsPrefab(e);
    }

    /// <summary>"Add Component" 下拉：列出所有组件类型（跳过已挂的），点选即加一个空组件。</summary>
    private void DrawAddComponent(int e)
    {
        if (ImGui.Button("Add Component", new Vector2(-1, 0)))
        {
            _addComponentFilter = "";
            ImGui.OpenPopup("##add_component_popup");
        }

        if (!ImGui.BeginPopup("##add_component_popup")) return;

        ImGui.SetNextItemWidth(240);
        ImGui.InputTextWithHint("##addfilter", "Filter types...", ref _addComponentFilter, 64);
        ImGui.Separator();

        ImGui.BeginChild("##add_list", new Vector2(280, 320));
        var all = EcsPoolUtil.AllComponentTypes;
        var pools = _world.AllPools;
        for (int i = 0; i < all.Count; i++)
        {
            var t = all[i];
            if (t == typeof(SpawnIdComp) || t == typeof(PrefabRefComp)) continue;
            if (HasComponent(pools, e, t)) continue;                        // 已挂的跳过
            if (!string.IsNullOrEmpty(_addComponentFilter) &&
                !t.Name.Contains(_addComponentFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ImGui.Selectable(t.Name))
            {
                EcsPoolUtil.AddEmptyComponent(_world, e, t);
                _status = $"Added {t.Name}.";
                ImGui.CloseCurrentPopup();
                break;
            }
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(t.Namespace))
                ImGui.SetTooltip(t.Namespace + "." + t.Name);
        }
        ImGui.EndChild();
        ImGui.EndPopup();
    }

    /// <summary>实体 e 是否已挂类型 type 的组件。遍历实体现有组件类型比对，不创建新 pool。</summary>
    private bool HasComponent(ReadOnlySpan<IEcsPool> pools, int e, Type type)
    {
        foreach (int cid in _world.GetComponentTypeIDsFor(e))
            if (pools[cid].ComponentType == type) return true;
        return false;
    }

    private void DrawSaveAsPrefab(int e)
    {
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("##prefabname", ref _newPrefabName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Save as Prefab"))
        {
            // 内容名带子目录前缀（Prefabs/xxx），与启动扫描器算的 AssetId 一致。
            // Id 不在此赋值：Register 会按 contentName 回填，保证存盘 id ≡ 注册 id。
            var contentName = PrefabName(_newPrefabName);
            var prefab = PrefabSerializer.Capture(_world, e, contentName);
            _assets.Register(contentName, prefab);                          // 进库并回填 Id，调色板立刻可见
            StorageUtils.GetDevGameRoot.SaveJson(PrefabPath(_newPrefabName), prefab, PrefabSerializer.Options);
            _status = $"Saved prefab '{contentName}'.";
        }
    }

    // ---- level file ops ------------------------------------------------

    private void ClearLevel()
    {
        var span = _world.ToSpan();
        for (int i = span.Count - 1; i >= 0; i--)
            _world.DelEntity(span[i]);
        _selectedEntity = -1;
    }

    private void SaveLevel()
    {
        var level = LevelSerializer.Save(_world, _levelName);
        StorageUtils.GetDevGameRoot.SaveJson(LevelPath(_levelName), level, PrefabSerializer.Options);
        _status = $"Saved '{LevelPath(_levelName)}' ({level.Entities.Count} entities).";
    }

    private void LoadLevel()
    {
        var path = LevelPath(_levelName);
        if (!StorageUtils.GetDevGameRoot.FileExists(path)) { _status = $"Not found: {path}"; return; }
        ClearLevel();
        var level = StorageUtils.GetDevGameRoot.LoadJson<LevelData>(path, PrefabSerializer.Options);
        LevelSerializer.Load(_world, level, _assets);
        _status = $"Loaded '{path}' ({level.Entities.Count} entities).";
    }
}
