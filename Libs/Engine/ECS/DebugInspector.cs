using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;
using Engine.DearImGui;
using ImGuiNET;

namespace Engine.ECS;

internal static class ComponentTypeCatalog
{
    private static Type[]? _cache;

    public static Type[] All
    {
        get
        {
            if (_cache != null) return _cache;
            var list = new List<Type>(256);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!t.IsValueType) continue;
                    if (t.IsGenericTypeDefinition) continue;
                    if (t.ContainsGenericParameters) continue;
                    if (!typeof(IEcsComponentMember).IsAssignableFrom(t)) continue;
                    list.Add(t);
                }
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _cache = list.ToArray();
            return _cache;
        }
    }

    public static void Invalidate() => _cache = null;
}

public sealed class EntlongDrawer : PropertyDrawer<entlong>
{
    protected override bool OnDraw(string label, ref entlong v)
    {
        ImGui.LabelText(label, v.ToString());
        return false;
    }
}


public class EcsInspector
{
    private readonly List<EcsWorld> _worlds = new();
    private int _selectedWorldIndex;
    private int _selectedEntityID = -1;
    private string _entityFilter = string.Empty;
    private string _componentFilter = string.Empty;
    private string _addComponentFilter = string.Empty;
    private readonly int[] _componentIdsBuffer = new int[256];
    private readonly HashSet<Type> _entityTypesBuffer = new();
    private bool _pendingScrollToSelected;
    private bool _pendingWindowFocus;

    public string WindowTitle { get; set; } = "ECS Inspector";
    public bool IsOpen = true;

    public IReadOnlyList<EcsWorld> Worlds => _worlds;

    public int SelectedEntityID => _selectedEntityID;
    public EcsWorld? SelectedWorld =>
        _worlds.Count == 0 ? null : _worlds[Math.Clamp(_selectedWorldIndex, 0, _worlds.Count - 1)];

    public void RegisterWorld(EcsWorld world)
    {
        if (world != null && !_worlds.Contains(world))
            _worlds.Add(world);
    }

    public void UnregisterWorld(EcsWorld world) => _worlds.Remove(world);

    /// <summary>
    /// 选中指定 World 里的某个实体。如 openWindow 为 true，会自动打开并聚焦窗口。
    /// </summary>
    public void Focus(EcsWorld world, int entityID, bool openWindow = true, bool scrollTo = true)
    {
        if (world == null) return;
        int idx = _worlds.IndexOf(world);
        if (idx < 0)
        {
            RegisterWorld(world);
            idx = _worlds.IndexOf(world);
            if (idx < 0) return;
        }
        _selectedWorldIndex = idx;
        _selectedEntityID = entityID;
        _entityFilter = string.Empty;
        if (scrollTo) _pendingScrollToSelected = true;
        if (openWindow)
        {
            IsOpen = true;
            _pendingWindowFocus = true;
        }
    }

    /// <summary>
    /// 使用 entlong 自带的 world 信息选中实体。
    /// </summary>
    public void Focus(entlong ent, bool openWindow = true, bool scrollTo = true)
    {
        if (!ent.TryUnpack(out int id, out EcsWorld w)) return;
        Focus(w, id, openWindow, scrollTo);
    }

    /// <summary>取消当前选中。</summary>
    public void ClearSelection() => _selectedEntityID = -1;

    public void Draw()
    {
        if (!IsOpen) return;

        if (_pendingWindowFocus)
        {
            ImGui.SetNextWindowFocus();
            _pendingWindowFocus = false;
        }

        if (!ImGui.Begin(WindowTitle, ref IsOpen))
        {
            ImGui.End();
            return;
        }

        DrawWorldSelector();
        ImGui.Separator();

        if (_worlds.Count == 0)
        {
            ImGui.TextDisabled("No worlds registered.");
            ImGui.End();
            return;
        }

        var world = _worlds[Math.Clamp(_selectedWorldIndex, 0, _worlds.Count - 1)];
        if (world.IsDestroyed)
        {
            ImGui.TextDisabled("World destroyed.");
            ImGui.End();
            return;
        }

        var avail = ImGui.GetContentRegionAvail();
        float leftW = MathF.Max(200f, avail.X * 0.35f);

        ImGui.BeginChild("##ecs_left", new Vector2(leftW, 0), ImGuiChildFlags.Borders);
        DrawEntityList(world);
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##ecs_right", new Vector2(0, 0), ImGuiChildFlags.Borders);
        DrawEntityDetail(world);
        ImGui.EndChild();

        ImGui.End();
    }

    private void DrawWorldSelector()
    {
        var names = _worlds.Select(w =>
            string.IsNullOrEmpty(w.Name) ? $"World[{w.ID}]" : $"{w.Name}[{w.ID}]").ToArray();
        int idx = _selectedWorldIndex;
        ImGui.SetNextItemWidth(220);
        if (names.Length > 0 && ImGui.Combo("World", ref idx, names, names.Length))
            _selectedWorldIndex = idx;

        if (_worlds.Count > 0)
        {
            var w = _worlds[Math.Clamp(_selectedWorldIndex, 0, _worlds.Count - 1)];
            ImGui.SameLine();
            ImGui.Text($"Alive: {w.Count}  Pools: {w.PoolsCount}");
        }
    }

    private void DrawEntityList(EcsWorld world)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##entfilter", "Filter entities...", ref _entityFilter, 64);

        var span = world.Entities;
        for (int i = 0; i < span.Count; i++)
        {
            int e = span[i];
            int compCount = world.GetComponentsCount(e);
            var label = $"Entity {e}  ({compCount})";
            if (!string.IsNullOrEmpty(_entityFilter) &&
                !label.Contains(_entityFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            bool isSelected = _selectedEntityID == e;
            if (isSelected && _pendingScrollToSelected)
            {
                ImGui.SetScrollHereY(0.5f);
                _pendingScrollToSelected = false;
            }
            if (ImGui.Selectable(label, isSelected))
                _selectedEntityID = e;
        }
    }

    private void DrawEntityDetail(EcsWorld world)
    {
        if (_selectedEntityID < 0 || !world.IsUsed(_selectedEntityID))
        {
            ImGui.TextDisabled("Select an entity");
            return;
        }

        ImGui.Text($"Entity ID: {_selectedEntityID}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Delete"))
        {
            world.DelEntity(_selectedEntityID);
            _selectedEntityID = -1;
            return;
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##cmpfilter", "Filter components...", ref _componentFilter, 64);
        ImGui.Separator();

        var componentIds = world.GetComponentTypeIDsFor(_selectedEntityID);
        int count = componentIds.Length;
        if (count > _componentIdsBuffer.Length) count = _componentIdsBuffer.Length;
        for (int i = 0; i < count; i++) _componentIdsBuffer[i] = componentIds[i];

        var pools = world.AllPools;
        _entityTypesBuffer.Clear();
        for (int i = 0; i < count; i++)
        {
            int id = _componentIdsBuffer[i];
            var pool = pools[id];
            var type = pool.ComponentType;
            _entityTypesBuffer.Add(type);

            if (!string.IsNullOrEmpty(_componentFilter) &&
                !type.Name.Contains(_componentFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            DrawComponent(world, _selectedEntityID, pool, type);
        }

        ImGui.Separator();
        DrawAddComponentSection(world, _selectedEntityID);
    }

    private void DrawAddComponentSection(EcsWorld world, int entity)
    {
        if (ImGui.Button("Add Component", new Vector2(-1, 0)))
        {
            _addComponentFilter = string.Empty;
            ImGui.OpenPopup("##add_component_popup");
        }

        if (ImGui.BeginPopup("##add_component_popup"))
        {
            ImGui.SetNextItemWidth(240);
            ImGui.InputTextWithHint("##addfilter", "Filter types...", ref _addComponentFilter, 64);
            ImGui.Separator();

            ImGui.BeginChild("##add_list", new Vector2(280, 320));
            var all = ComponentTypeCatalog.All;
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (_entityTypesBuffer.Contains(t)) continue;
                if (!string.IsNullOrEmpty(_addComponentFilter) &&
                    !t.Name.Contains(_addComponentFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ImGui.Selectable(t.Name))
                {
                    TryAddComponent(world, entity, t);
                    ImGui.CloseCurrentPopup();
                    break;
                }
                if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(t.Namespace))
                    ImGui.SetTooltip(t.Namespace + "." + t.Name);
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }
    }

    private static readonly Dictionary<Type, MethodInfo> _getPoolInstanceCache = new();
    private static readonly MethodInfo _getPoolInstanceOpen =
        typeof(EcsWorld).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == "GetPoolInstance" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

    private static IEcsPool? GetOrCreatePoolFor(EcsWorld world, Type componentType)
    {
        Type poolType;
        if (typeof(IEcsTagComponent).IsAssignableFrom(componentType))
            poolType = typeof(EcsTagPool<>).MakeGenericType(componentType);
        else if (typeof(IEcsValueComponent).IsAssignableFrom(componentType))
            poolType = typeof(EcsValuePool<>).MakeGenericType(componentType);
        else
            poolType = typeof(EcsPool<>).MakeGenericType(componentType);

        if (!_getPoolInstanceCache.TryGetValue(poolType, out var mi))
        {
            mi = _getPoolInstanceOpen.MakeGenericMethod(poolType);
            _getPoolInstanceCache[poolType] = mi;
        }
        return mi.Invoke(world, null) as IEcsPool;
    }

    private static void TryAddComponent(EcsWorld world, int entity, Type type)
    {
        try
        {
            var pool = GetOrCreatePoolFor(world, type);
            if (pool == null)
            {
                EcsDebug.PrintWarning($"Add component {type.Name} failed: cannot resolve pool.");
                return;
            }
            if (!pool.Has(entity))
                pool.AddEmpty(entity);
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            EcsDebug.PrintWarning($"Add component {type.Name} failed: {tie.InnerException.Message}");
        }
        catch (Exception ex)
        {
            EcsDebug.PrintWarning($"Add component {type.Name} failed: {ex.Message}");
        }
    }

    private static readonly Type _tagInterface = typeof(IEcsTagComponent);

    private static void DrawComponent(EcsWorld world, int entity, IEcsPool pool, Type type)
    {
        ImGui.PushID(type.FullName ?? type.Name);

        bool isTag = _tagInterface.IsAssignableFrom(type);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        bool isEmptyStruct = fields.Length == 0;

        string headerLabel = (isTag || isEmptyStruct)
            ? type.Name + "   [Tag]"
            : type.Name;

        var flags = ImGuiTreeNodeFlags.AllowOverlap;
        if (isTag || isEmptyStruct)
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Bullet;
        else
            flags |= ImGuiTreeNodeFlags.DefaultOpen;

        // 记录 Header 前的光标位置和可用宽度，用于将 X 按钮对齐到同行右侧
        float headerY = ImGui.GetCursorPosY();
        float headerStartX = ImGui.GetCursorPosX();
        float availX = ImGui.GetContentRegionAvail().X;
        float rightX = headerStartX + availX - 22f;

        bool open = ImGui.CollapsingHeader(headerLabel, flags);

        // 将按钮定位到 Header 右侧（相同 Y）
        ImGui.SameLine();
        ImGui.SetCursorPosX(rightX);
        ImGui.SetCursorPosY(headerY);

        bool removed = false;
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.15f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0.2f, 0.2f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.25f, 0.25f, 1f));
        if (ImGui.SmallButton("X##remove"))
        {
            try { pool.Del(entity); removed = true; }
            catch (Exception ex) { EcsDebug.PrintWarning($"Del {type.Name} failed: {ex.Message}"); }
        }
        ImGui.PopStyleColor(3);

        if (open && !removed && !isTag && !isEmptyStruct)
        {
            ImGui.Indent();
            object? raw;
            try { raw = pool.GetRaw(entity); }
            catch { raw = null; }

            if (raw == null)
            {
                ImGui.TextDisabled("(null)");
            }
            else
            {
                if (ReflectionStructDrawer.DrawStructBody(ref raw, type))
                {
                    try { pool.SetRaw(entity, raw!); }
                    catch (Exception ex) { ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), ex.Message); }
                }
            }
            ImGui.Unindent();
        }
        ImGui.PopID();
    }
}



public sealed class EcsInspectorSystem : IUpdateSystem, IEcsInject<EcsWorld>
{
    private readonly EcsInspector _inspector;
    private readonly Action? _registerDrawersCallback;
    public EcsInspector Inspector => _inspector;
    public EcsInspectorSystem() : this(new EcsInspector(), null) { }
    public EcsInspectorSystem(Action registerDrawers) : this(new EcsInspector(), registerDrawers) { }
    public EcsInspectorSystem(EcsInspector inspector) : this(inspector, null) { }
    public EcsInspectorSystem(EcsInspector inspector, Action? registerDrawers)
    {
        _inspector = inspector;
        _registerDrawersCallback = registerDrawers;
        PropertyDrawerRegistry.Register(new EntlongDrawer());
        _registerDrawersCallback?.Invoke();
    }

    public void Inject(EcsWorld world)
    {
        _inspector.RegisterWorld(world);
    }

    public void Update()
    {
        ProcessDebugInspectorComponents();
        _inspector.Draw();
    }

    /// <summary>
    /// 获取指定 world 上唯一的 DebugInspectorComponent 引用（利用 DragonECS 的 world-scoped component）。
    /// 其它系统可以直接写入 Target = ent 来触发聚焦。
    /// </summary>
    public static ref DebugInspectorComponent GetSingleton(EcsWorld world)
    {
        return ref world.Get<DebugInspectorComponent>();
    }

    public static void FocusEntity(EcsWorld world, entlong entity)
    {
       ref var debug = ref world.Get<DebugInspectorComponent>();
       debug.Target = entity;
    }

    private void ProcessDebugInspectorComponents()
    {
        var worlds = _inspector.Worlds;
        for (int wi = 0; wi < worlds.Count; wi++)
        {
            var w = worlds[wi];
            if (w.IsDestroyed) continue;
            if (!w.Has<DebugInspectorComponent>()) continue;

            ref var cmp = ref w.GetUnchecked<DebugInspectorComponent>();
            if (!cmp.Target.TryUnpack(out int targetID, out EcsWorld targetWorld)) continue;
            bool open = !cmp.NoOpenWindow;
            bool scroll = !cmp.NoScrollTo;
            _inspector.Focus(targetWorld, targetID, open, scroll);
            if (!cmp.Sticky) cmp.Target = default;
        }
    }
}
        
/// <summary>
/// World-scoped 单例组件（使用 EcsWorld.Get&lt;T&gt;() 存取）：每个 world 内自然唯一，
/// 不需要实体也不进普通 Pool。其他系统写入 Target = ent 即可让 Inspector 聚焦。
/// </summary>
public struct DebugInspectorComponent
{
    /// <summary>目标实体，写入后下一帧会被 Inspector 消费。default 为无。</summary>
    public entlong Target;
    /// <summary>不自动打开 / 聚焦 Inspector 窗口。</summary>
    public bool NoOpenWindow;
    /// <summary>不滚动列表到当前实体。</summary>
    public bool NoScrollTo;
    /// <summary>处理后保留 Target（默认会清空，避免每帧重复聚焦）。</summary>
    public bool Sticky;
}