using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using DCFApixels.DragonECS;
using Engine;
using Engine.Assets;
using Engine.DearImGui;
using Engine.ECS;
using Engine.World;
using Foster.Framework;
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
/// 简易 Tile 关卡编辑器（ImGui + 场景视口）：新建 / 保存 / 加载关卡，从预制体生成实体，编辑组件字段，
/// 把选中实体反存成预制体。复用 Engine 侧的 LevelSerializer / PrefabSerializer / AssetDatabase。
///
/// 场景交互（鼠标在 ImGui 窗口之外时才生效，避免和面板抢输入）：
///   · 选中调色板里的预制体作为"笔刷"，左键在网格上按格摆放（拖动可连续刷），右键擦除该格实体；
///   · 未选笔刷时左键点击 = 拾取光标下实体到右侧 Inspector；
///   · 中键拖动 = 平移相机，滚轮 = 缩放。
/// 摆放会把实体的 PositionComp 吸附到网格中心，一格一个实体（重复摆放会覆盖旧的）。
///
/// 依赖由 AutoInject 注入：编辑器世界 / 资产库 / 场景 batcher / 相机 / 原始输入 / 窗口。
/// 用法（Program.cs）：编辑器 pipeline 里 .Inject(Input).Inject(Window) 后 .AddModule(new EditorModule())
/// </summary>
public sealed class LevelEditorSystem : IUpdateSystem, IRenderSystem
{
    [DI] private EcsEditorWorld _world;
    [DI] private AssetDatabase _assets;
    [DI] private Batcher _batcher;
    [DI] private Camera2D _camera;
    [DI] private Input _input;
    [DI] private Window _window;

    public bool IsOpen = true;
    public string WindowTitle = "关卡编辑器";

    private string _levelName = "untitled";
    private string _newPrefabName = "new_prefab";
    private int _selectedEntity = -1;
    private string _status = "";
    private string _addComponentFilter = "";

    // ---- Tile / 视口编辑态 ----
    // 当前笔刷预制体名（null = 无笔刷，此时左键用于拾取实体）。
    private string? _brushPrefab;
    // 网格格边长（世界单位）。摆放时把实体吸附到 cell 中心。
    private float _gridSize = 1f;
    // 是否画网格线。
    private bool _showGrid = true;
    // 记录本次拖动已经刷过的格子，避免一次拖动在同一格反复生成 / 抖动。
    private Vector2Int _lastPaintedCell;
    private bool _hasPaintedThisDrag;
    // Update 里缓存的 "鼠标是否悬在 ImGui 窗口上"。Render 阶段 ImGui 帧已结束，不能再调 ImGui.GetIO()，
    // 所以在这里存一份给 Render 用。
    private bool _mouseOverUi;

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
        // 场景交互先跑：即便面板关掉了，视口里的拖拽摆放依然可用。
        HandleViewportInput();

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

    // ---- 视口交互（相机 + Tile 摆放 / 擦除 / 拾取） --------------------

    /// <summary>
    /// 处理场景视口里的鼠标：平移 / 缩放 / 摆放 / 擦除 / 拾取。
    /// 鼠标悬在任意 ImGui 窗口上时整段跳过（ImGui 优先），避免点面板时误刷格子。
    /// </summary>
    private void HandleViewportInput()
    {
        var io = ImGui.GetIO();
        var mouse = _input.Mouse;
        _mouseOverUi = io.WantCaptureMouse;   // 给 Render 阶段用（那时 ImGui 帧已结束）

        // 缩放：滚轮以固定步长缩放（不做屏幕锚点，够用即可）。面板上滚动时交给 ImGui。
        if (!io.WantCaptureMouse && mouse.Wheel.Y != 0)
            _camera.Zoom = Math.Clamp(_camera.Zoom * (1f + mouse.Wheel.Y * 0.1f), 0.1f, 10f);

        // 平移：中键拖动，按屏幕位移换算成世界位移（除以 PPU*Zoom）。
        if (mouse.MiddleDown && !io.WantCaptureMouse)
        {
            float unitsPerPixel = 1f / (_camera.PPU * _camera.Zoom);
            _camera.Position -= mouse.Delta * unitsPerPixel;
        }

        bool overUi = io.WantCaptureMouse;

        // 左键：有笔刷 → 摆放；无笔刷 → 拾取。
        // 笔刷是 Tile → 按住拖动连续刷（一格一个）；是自由实体 → 每次点击落一个。
        if (!overUi && mouse.LeftDown)
        {
            var world = ScreenToWorld(mouse.Position);
            if (_brushPrefab != null)
                PaintAt(world, mouse.LeftPressed);
            else if (mouse.LeftPressed)
                PickAt(world);
        }
        else
        {
            _hasPaintedThisDrag = false;   // 松开左键，结束本次拖动
        }

        // 右键：擦除光标下的实体。
        if (!overUi && mouse.RightPressed)
            EraseAt(ScreenToWorld(mouse.Position));
    }

    /// <summary>像素坐标 → 世界坐标：相机矩阵求逆后变换。</summary>
    private Vector2 ScreenToWorld(Vector2 pixel)
    {
        var m = _camera.GetMatrix(_window);
        return Matrix3x2.Invert(m, out var inv) ? Vector2.Transform(pixel, inv) : pixel;
    }

    /// <summary>世界坐标 → 网格格号（floor 到格）。</summary>
    private Vector2Int WorldToCell(Vector2 world) =>
        new((int)MathF.Floor(world.X / _gridSize), (int)MathF.Floor(world.Y / _gridSize));

    /// <summary>格号 → 格中心的世界坐标（Tile 摆在格中心）。</summary>
    private Vector2 CellCenter(Vector2Int cell) =>
        new((cell.X + 0.5f) * _gridSize, (cell.Y + 0.5f) * _gridSize);

    /// <summary>预制体是否为 Tile（组件里带 TileComp）。据此决定摆放是吸附网格还是精确落点。</summary>
    private static bool IsTilePrefab(Prefab prefab) => prefab.Components.ContainsKey(nameof(TileComp));

    /// <summary>
    /// 摆放当前笔刷预制体。
    /// Tile：吸附到格中心、一格一个（该格已有则覆盖），拖动可连续刷。
    /// 自由实体：落在光标精确位置、可重叠，仅在按下那一下生成（拖动不连发）。
    /// </summary>
    private void PaintAt(Vector2 world, bool isFirstClick)
    {
        if (_brushPrefab == null) return;
        var prefab = _assets.Get<Prefab>(_brushPrefab);
        if (prefab == null) { _status = $"Brush prefab '{_brushPrefab}' missing."; return; }

        if (IsTilePrefab(prefab))
        {
            var cell = WorldToCell(world);
            // 同一次拖动、同一格：跳过，避免抖动式重复生成。
            if (_hasPaintedThisDrag && !isFirstClick && cell == _lastPaintedCell) return;
            _lastPaintedCell = cell;
            _hasPaintedThisDrag = true;

            // 覆盖语义：该格若已有 Tile，先删旧的。
            int existing = FindTileInCell(cell);
            if (existing >= 0) _world.DelEntity(existing);

            int e = PrefabSerializer.Instantiate(_world, prefab);
            SetPosition(e, CellCenter(cell));
            _selectedEntity = e;
            _status = $"Placed tile '{_brushPrefab}' at ({cell.X}, {cell.Y}).";
        }
        else
        {
            // 自由实体：只在按下那一下落一个，拖动不连发（避免一路撒一堆）。
            if (!isFirstClick) return;
            int e = PrefabSerializer.Instantiate(_world, prefab);
            SetPosition(e, world);
            _selectedEntity = e;
            _status = $"Placed entity '{_brushPrefab}' at ({world.X:F2}, {world.Y:F2}).";
        }
    }

    /// <summary>擦除：删掉光标下的实体（先命中自由实体的包围盒，再退回该格 Tile）。</summary>
    private void EraseAt(Vector2 world)
    {
        int e = HitTest(world);
        if (e < 0) return;
        _world.DelEntity(e);
        if (_selectedEntity == e) _selectedEntity = -1;
        _status = "Erased.";
    }

    /// <summary>拾取：选中光标下的实体到 Inspector。</summary>
    private void PickAt(Vector2 world)
    {
        int e = HitTest(world);
        _selectedEntity = e;   // 没命中则 -1，Inspector 显示"未选中"
        if (e >= 0) _status = $"Selected #{e}.";
    }

    /// <summary>
    /// 命中测试：返回光标下最该选中的关卡实体。
    /// 优先自由实体（按 RenderComp 包围盒，后画的在上、优先命中）；否则退回光标所在格的 Tile。
    /// </summary>
    private int HitTest(Vector2 world)
    {
        var spawnPool = _world.GetPool<SpawnIdComp>();
        var posPool = _world.GetPool<PositionComp>();
        var tilePool = _world.GetPool<TileComp>();
        var renderPool = _world.GetPool<RenderComp>();

        int hit = -1;
        foreach (int e in _world.Entities)
        {
            if (!spawnPool.Has(e) || !posPool.Has(e) || tilePool.Has(e)) continue;  // 只测自由实体
            var pos = posPool.Get(e).Value;
            var half = (renderPool.Has(e) ? renderPool.Get(e).Size : Vector2.One * _gridSize) * 0.5f;
            if (half.X <= 0) half.X = _gridSize * 0.5f;
            if (half.Y <= 0) half.Y = _gridSize * 0.5f;
            if (MathF.Abs(world.X - pos.X) <= half.X && MathF.Abs(world.Y - pos.Y) <= half.Y)
                hit = e;  // 不 break：取遍历中最后一个（画在最上层）
        }
        if (hit >= 0) return hit;

        return FindTileInCell(WorldToCell(world));  // 没有自由实体命中 → 退回格内 Tile
    }

    /// <summary>找某格里的 Tile 实体（带 TileComp + 位置落在该格）；没有返回 -1。</summary>
    private int FindTileInCell(Vector2Int cell)
    {
        var spawnPool = _world.GetPool<SpawnIdComp>();
        var posPool = _world.GetPool<PositionComp>();
        var tilePool = _world.GetPool<TileComp>();
        foreach (int e in _world.Entities)
        {
            if (!spawnPool.Has(e) || !posPool.Has(e) || !tilePool.Has(e)) continue;
            if (WorldToCell(posPool.Get(e).Value) == cell) return e;
        }
        return -1;
    }

    /// <summary>写实体位置：没有 PositionComp 就补一个（要画出来一定要有位置）。</summary>
    private void SetPosition(int e, Vector2 pos)
    {
        var posPool = _world.GetPool<PositionComp>();
        if (!posPool.Has(e)) posPool.Add(e);
        posPool.Get(e).Value = pos;
    }

    // ---- 场景渲染（世界空间；相机矩阵已由 Program.Render 压栈） --------

    /// <summary>
    /// 画视口：网格线 + 每个带位置的实体一个色块 + 选中描边 + 笔刷落点预览。
    /// 预制体本身没有 sprite，这里用格子填色代表一个 tile；颜色按预制体名散列，便于区分种类。
    /// </summary>
    public void Render()
    {
        if (_showGrid) DrawGrid();

        var spawnPool = _world.GetPool<SpawnIdComp>();
        var posPool = _world.GetPool<PositionComp>();
        var tilePool = _world.GetPool<TileComp>();
        var renderPool = _world.GetPool<RenderComp>();

        // 两趟：先画 Tile（铺底），再画自由实体（叠在上层），选中描边最后单独补。
        foreach (int e in _world.Entities)
        {
            if (!spawnPool.Has(e) || !posPool.Has(e)) continue;
            var pos = posPool.Get(e).Value;
            bool hasRender = renderPool.Has(e);
            var rc = hasRender ? renderPool.Get(e) : default;
            Color color = hasRender ? FromVec4(rc.Color) : Color.Gray;

            if (tilePool.Has(e))
            {
                // Tile：填满所在格（留一点内边看清网格线）。颜色来自 RenderComp，没有则灰。
                var cell = WorldToCell(pos);
                float s = _gridSize;
                float inset = s * 0.06f;
                _batcher.Rect(new Vector2(cell.X * s + inset, cell.Y * s + inset),
                              new Vector2(s - inset * 2, s - inset * 2), color);
            }
            else
            {
                // 自由实体：按 RenderComp 的形状 / 尺寸画在精确位置。无 RenderComp 时给个默认小方块。
                var size = (hasRender && rc.Size != Vector2.Zero) ? rc.Size : Vector2.One * (_gridSize * 0.6f);
                if (hasRender && rc.Shape == RenderShape.Circle)
                    _batcher.Circle(pos, MathF.Max(0.01f, size.X * 0.5f), 24, color);
                else
                    _batcher.Rect(pos - size * 0.5f, size, color);
            }
        }

        // 选中描边（单独一趟，保证画在最上层）。
        if (_selectedEntity >= 0 && _world.IsUsed(_selectedEntity) && posPool.Has(_selectedEntity))
            DrawSelectionOutline(_selectedEntity, posPool, tilePool, renderPool);

        DrawBrushGhost();
    }

    /// <summary>给选中实体描一圈白框：Tile 描格，自由实体描其包围盒。</summary>
    private void DrawSelectionOutline(int e, EcsPool<PositionComp> posPool, EcsTagPool<TileComp> tilePool, EcsPool<RenderComp> renderPool)
    {
        var pos = posPool.Get(e).Value;
        float w = MathF.Max(0.02f, _gridSize * 0.04f);
        if (tilePool.Has(e))
        {
            var cell = WorldToCell(pos);
            float s = _gridSize;
            _batcher.RectLine(new Rect(cell.X * s, cell.Y * s, s, s), w, Color.White);
        }
        else
        {
            var size = renderPool.Has(e) && renderPool.Get(e).Size != Vector2.Zero
                ? renderPool.Get(e).Size : Vector2.One * (_gridSize * 0.6f);
            _batcher.RectLine(new Rect((pos - size * 0.5f).X, (pos - size * 0.5f).Y, size.X, size.Y), w, Color.White);
        }
    }

    /// <summary>
    /// 笔刷落点预览：Tile 高亮整格，自由实体在光标处画半透明形状。
    /// 用 Update 缓存的 _mouseOverUi，不在 Render 里碰 ImGui（此时帧已结束）。
    /// </summary>
    private void DrawBrushGhost()
    {
        if (_brushPrefab == null || _mouseOverUi) return;
        var prefab = _assets.Get<Prefab>(_brushPrefab);
        if (prefab == null) return;

        var world = ScreenToWorld(_input.Mouse.Position);
        var (color, shape, size) = PreviewLook(prefab);
        var ghost = color; ghost.A = 100;

        if (IsTilePrefab(prefab))
        {
            var cell = WorldToCell(world);
            float s = _gridSize;
            _batcher.Rect(new Vector2(cell.X * s, cell.Y * s), new Vector2(s, s), ghost);
            _batcher.RectLine(new Rect(cell.X * s, cell.Y * s, s, s), MathF.Max(0.02f, s * 0.04f), Color.Yellow);
        }
        else
        {
            if (shape == RenderShape.Circle)
                _batcher.Circle(world, MathF.Max(0.01f, size.X * 0.5f), 24, ghost);
            else
                _batcher.Rect(world - size * 0.5f, size, ghost);
        }
    }

    /// <summary>从预制体的 RenderComp 默认值解析出预览用的颜色 / 形状 / 尺寸；缺省时给合理默认。</summary>
    private (Color color, RenderShape shape, Vector2 size) PreviewLook(Prefab prefab)
    {
        Color color = Color.Gray;
        var shape = RenderShape.Rect;
        var size = Vector2.One * (_gridSize * 0.6f);
        if (prefab.Components.TryGetValue(nameof(RenderComp), out var elem))
        {
            try
            {
                var rc = elem.Deserialize<RenderComp>(PrefabSerializer.Options);
                color = FromVec4(rc.Color);
                shape = rc.Shape;
                if (rc.Size != Vector2.Zero) size = rc.Size;
            }
            catch { /* 预制体里 RenderComp 结构不匹配时用默认外观 */ }
        }
        return (color, shape, size);
    }

    /// <summary>Vector4(RGBA 0..1) → Foster Color。分量夹到 [0,1]。</summary>
    private static Color FromVec4(Vector4 c) => new(
        Math.Clamp(c.X, 0f, 1f), Math.Clamp(c.Y, 0f, 1f),
        Math.Clamp(c.Z, 0f, 1f), Math.Clamp(c.W, 0f, 1f));

    /// <summary>画一片覆盖当前视野的网格线（按相机可见范围推算格子区间）。</summary>
    private void DrawGrid()
    {
        // 视野半宽/半高（世界单位）= 屏幕半像素 / (PPU*Zoom)。
        float unitsPerPixel = 1f / (_camera.PPU * _camera.Zoom);
        float halfW = _window.WidthInPixels * 0.5f * unitsPerPixel;
        float halfH = _window.HeightInPixels * 0.5f * unitsPerPixel;
        var center = _camera.Position;

        float s = _gridSize;
        int x0 = (int)MathF.Floor((center.X - halfW) / s) - 1;
        int x1 = (int)MathF.Ceiling((center.X + halfW) / s) + 1;
        int y0 = (int)MathF.Floor((center.Y - halfH) / s) - 1;
        int y1 = (int)MathF.Ceiling((center.Y + halfH) / s) + 1;

        // 上限保护：极端缩小时避免画出海量线条。
        if ((x1 - x0) > 512 || (y1 - y0) > 512) return;

        var line = new Color(0x40, 0x48, 0x52, 0xff);
        var axis = new Color(0x70, 0x80, 0x90, 0xff);
        float w = MathF.Max(0.01f, s * 0.02f);

        for (int x = x0; x <= x1; x++)
            _batcher.Line(new Vector2(x * s, y0 * s), new Vector2(x * s, y1 * s), w, x == 0 ? axis : line);
        for (int y = y0; y <= y1; y++)
            _batcher.Line(new Vector2(x0 * s, y * s), new Vector2(x1 * s, y * s), w, y == 0 ? axis : line);
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
        // 网格 / 笔刷设置行。
        ImGui.SetNextItemWidth(120);
        ImGui.DragFloat("Grid", ref _gridSize, 0.05f, 0.1f, 16f);
        ImGui.SameLine();
        ImGui.Checkbox("Show grid", ref _showGrid);
        ImGui.SameLine();
        ImGui.TextDisabled(_brushPrefab == null ? "Brush: (none — click picks)" : $"Brush: {_brushPrefab}");

        ImGui.TextWrapped("Prefabs (select a brush, then drag in the scene to paint. Right-click erases, middle-drag pans, wheel zooms):");

        // "None" 笔刷：切回拾取模式。
        bool noneSelected = _brushPrefab == null;
        if (ImGui.RadioButton("None (pick)", noneSelected)) _brushPrefab = null;

        var names = _assets.NamesOf<Prefab>();
        if (names.Count == 0)
        {
            ImGui.TextDisabled("(no prefabs registered — use 'Save as Prefab' below)");
            return;
        }
        for (int i = 0; i < names.Count; i++)
        {
            var name = names[i];
            bool selected = _brushPrefab == name;
            // 选中的笔刷高亮显示。
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.45f, 0.85f, 1f));
            if (ImGui.Button(name))
                _brushPrefab = selected ? null : name;   // 再点一次取消选择
            if (selected) ImGui.PopStyleColor();
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
