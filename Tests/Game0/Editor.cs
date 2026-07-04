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
    [DI] private EcsDefaultWorld _world;
    [DI] private AssetDatabase _assets;

    public bool IsOpen = true;
    public string WindowTitle = "Level Editor";

    private string _levelName = "untitled";
    private string _newPrefabName = "new_prefab";
    private int _selectedEntity = -1;
    private string _status = "";
    
    private static string LevelPath(string name) => "Levels/" + name + ".json";
    private static string PrefabPath(string name) => "Prefabs/" + name + ".json";

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
        if (!string.IsNullOrEmpty(_status)) ImGui.TextDisabled(_status);
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
            if (!spawnPool.Has(e) || !prefabPool.Has(e)) continue;
            var id = spawnPool.Get(e).Id;
            bool selected = _selectedEntity == e;
            if (ImGui.Selectable($"#{e}  {id.ToHex()}", selected))
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
        DrawSaveAsPrefab(e);
    }

    private void DrawSaveAsPrefab(int e)
    {
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("##prefabname", ref _newPrefabName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Save as Prefab"))
        {
            var id = AssetId.FromName(_newPrefabName);
            var prefab = PrefabSerializer.Capture(_world, e, id, _newPrefabName);
            _assets.Register(_newPrefabName, prefab);                       // 进库，调色板立刻可见
            StorageUtils.GetDevGameRoot.SaveJson(PrefabPath(_newPrefabName), prefab, PrefabSerializer.Options);
            _status = $"Saved prefab '{_newPrefabName}'.";
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
