# Paper UI 使用指南

DragonLib 里的 **Paper**（Prowl.PaperUI）是一套即时模式（immediate-mode）的界面库，
经 `FosterCanvasRenderer` 把绘制命令桥接到 Foster 的图形管线。本文说明如何在游戏里接入、
每帧驱动，以及常用的布局 / 控件 API。

配套文件：

| 文件 | 职责 |
| --- | --- |
| `FosterCanvasRenderer.cs` | Prowl.Quill → Foster 画布后端（顶点 / 纹理 / shader 上传） |
| `PaperInput.cs` | 把 Foster 的鼠标 / 键盘输入转发给 Paper |
| `Shaders/QuillCanvas.hlsl` | 画布 shader 源码（纯色 / 图片 brush / SDF 文字三条路径） |
| `Shaders/Compiled/**` | 预编译 shader，随程序集以 `EmbeddedResource` 内嵌 |

---

## 1. 三个核心对象

接入 Paper 只需三样东西，通常在 `GameApp` 构造函数里创建一次：

```csharp
using Engine.Paper;
using Prowl.PaperUI;
using Prowl.Quill;
using Prowl.Scribe;

private FosterCanvasRenderer _paperRenderer = null!;
private Paper _paper = null!;
private FontFile _paperFont = null!;
private Point2 _paperResolution;

public MyGame(in AppConfig config) : base(in config)
{
    // 1) 渲染后端：把 Paper 的绘制命令画到窗口
    _paperRenderer = new FosterCanvasRenderer(this);

    // 2) Paper 主体：分辨率用「像素」，与窗口一致
    _paperResolution = new Point2(Window.WidthInPixels, Window.HeightInPixels);
    _paper = new Paper(_paperRenderer, _paperResolution.X, _paperResolution.Y, new FontAtlasSettings());

    // 3) 字体：从 ttf 字节构造，供所有 .Text(...) 调用复用
    byte[] fontData = StorageUtils.GetDevGameRoot.ReadAllBytes("Resources/Fonts/MapleMono-CN-Medium.ttf");
    _paperFont = new FontFile(fontData);
}
```

> **分辨率约定**：本工程约定 Paper 用**像素**分辨率（`Window.WidthInPixels`），
> 对应 `BeginFrame(dpiScale: 1f)` 与 `PaperInput.Update(scale: 1f)`。
> 三者必须一致，否则鼠标命中、图片采样都会错位。

---

## 2. 每帧驱动

Paper 是即时模式：**每一帧都要重新声明整个 UI**。驱动分两段——
逻辑（`Update`）里 `BeginFrame` + 构建 UI，绘制（`Render`）里 `EndFrame` 出图。

### Update：转发输入 → BeginFrame → 构建 UI

```csharp
protected override void Update()
{
    // 窗口尺寸变化时同步给 Paper（否则布局按旧分辨率算）
    var size = new Point2(Window.WidthInPixels, Window.HeightInPixels);
    if (size != _paperResolution)
    {
        _paperResolution = size;
        _paper.SetResolution(size.X, size.Y);
    }

    // 必须在 BeginFrame 之前转发本帧输入
    PaperInput.Update(_paper, this);
    _paper.BeginFrame(Time.Delta, dpiScale: 1f);

    // 文本输入：Paper 想要键盘时打开 IME
    if (_paper.WantsCaptureKeyboard)
        Window.StartTextInput();
    else
        Window.StopTextInput();

    // 在这里声明 UI（见第 3 节）。即时模式，每帧都要重新调用。
    BuildUI();
}
```

### Render：EndFrame 出图

```csharp
protected override void Render()
{
    Window.Clear(Color.AliceBlue);

    // 先画世界 / 其它内容 ...

    // EndFrame 走 FosterCanvasRenderer，把 UI 叠在最上层
    _paper.EndFrame();
}
```

> **顺序**：`BeginFrame`（Update）→ 声明 UI → `EndFrame`（Render）。
> UI 会覆盖在这一帧之前所有绘制之上。

### 释放

```csharp
protected override void Shutdown()
{
    _paperRenderer?.Dispose();   // 释放 mesh / material / 内部纹理
    // 你自己 new 出来的图片 Texture 也要各自 Dispose
}
```

---

## 3. 声明 UI：布局与控件

Paper 用**链式调用**声明元素。分两类：

- **容器**（`Row` / `Column`）：需要 `.Enter()` 且用 `using` 包住子元素。
- **叶子**（`Box`）：一条链声明完，不需要 `using`。

```csharp
private void BuildUI()
{
    // 容器：用 using + Enter()，子元素写在 { } 里
    using (_paper.Column("Root")
               .BackgroundColor(C(10, 16, 30))
               .Padding(24)
               .Enter())
    {
        // 叶子：一条链结束即可
        _paper.Box("Title")
            .Height(32)
            .Text("DRAGON CONTROL CENTER", _paperFont)
            .TextColor(C(240, 245, 255))
            .FontSize(24)
            .Alignment(TextAlignment.MiddleLeft);

        using (_paper.Row("Toolbar").Height(48).Enter())
        {
            // Row 内子元素横向排列 ...
        }
    }
}

// 便捷构造颜色（Prowl.Vector.Color）
private static Prowl.Vector.Color C(byte r, byte g, byte b, byte a = 255) => new(r, g, b, a);
```

### 元素 ID

每个元素第一个参数是**字符串 ID**，同一父级下必须唯一。
循环里生成时，用带索引的重载区分：

```csharp
for (var i = 0; i < items.Length; i++)
    _paper.Box("Item", i)      // ("Item", 0), ("Item", 1)... 各自独立
        .Height(46)
        .Text(items[i], _paperFont);
```

### 尺寸

| 写法 | 含义 |
| --- | --- |
| `.Width(220)` / `.Height(78)` | 固定像素 |
| `.Size(w, h)` | 一次设宽高 |
| `.Width(_paper.Stretch())` | 占满父级剩余空间（可多个平分） |
| `.Width(_paper.Percent(80))` | 父级宽度的百分比 |

### 间距、背景、边框、圆角

```csharp
.Padding(24)              // 四边
.Padding(22, 14)          // 横向, 纵向
.Padding(12, 0)           // 左右 12，上下 0
.Margin(0, 16, 0, 0)      // 左, 右, 上, 下
.BackgroundColor(C(20, 28, 48))
.BorderColor(C(51, 65, 92))
.BorderWidth(1)
.Rounded(18)              // 圆角半径
.Clip()                   // 裁剪超出自身范围的子内容（图片/文字溢出时用）
```

### 文字

```csharp
_paper.Box("Label")
    .Text("最近操作：等待中", _paperFont)   // 必须传字体
    .TextColor(C(139, 158, 190))
    .FontSize(14)
    .Alignment(TextAlignment.MiddleLeft);   // MiddleCenter / MiddleRight ...
```

> 建议 `using PaperAlign = Prowl.PaperUI.TextAlignment;` 简化写法。

### 图片

```csharp
_paper.Box("Poster")
    .Size(140, 80)
    .Clip()                                        // 建议配合裁剪
    .Image(myTexture, scaleMode: ImageScaleMode.Fit);
```

图片纹理是 Foster 的 `Texture`，从 PNG 加载：

```csharp
using var stream = StorageUtils.GetDevGameRoot.OpenRead("Resources/Images/Poster.png");
using var image = new Foster.Framework.Image(stream);
var texture = new Texture(GraphicsDevice, image, name: "Poster");
```

- `ImageScaleMode.Fit`：保持宽高比，完整装入（可能留白）。
- 框的宽高比和图差太多时，`Fit` 会明显留白——按图片比例设框尺寸更好看。

### 交互：hover / active / 点击

```csharp
_paper.Box("DeployButton")
    .Size(166, 46)
    .BackgroundColor(C(79, 70, 229))
    .Rounded(10)
    .Text("发布新版本", _paperFont)
    .TextColor(Prowl.Vector.Color.White)
    .Alignment(TextAlignment.MiddleCenter)
    .Hovered.BackgroundColor(C(99, 102, 241)).End()   // 悬停态，.End() 收尾
    .Active.BackgroundColor(C(67, 56, 202)).End()     // 按下态
    .OnClick(_ => _deployments++);                     // 点击回调
```

循环里带参数的点击，避免闭包捕获循环变量：

```csharp
.OnClick(index, (i, _) => _selected = i);
```

---

## 4. 图集与九宫格（SpriteAtlas / Nine-slice）

用一张图集（spritesheet）里的子图给 UI 贴皮：面板、按钮、进度条、图标。
相关类都在引擎层 `Libs/Engine/Rendering/`（图集）与 `Libs/Engine/Paper/PaperSprite.cs`（绘制）。

### 加载图集

图集 = 一张 PNG + 一份坐标（哪个子图在图集的哪个像素矩形）。坐标格式**可插拔**：
核心 `SpriteAtlas` 不认识任何具体格式，由 `ISpriteAtlasSource` 的实现负责解析。

```csharp
using Engine;   // SpriteAtlas / KenneyXmlAtlasSource

// Kenney / Starling 风格 XML（<TextureAtlas><SubTexture name x y width height/>）
var atlas = KenneyXmlAtlasSource.Load(GraphicsDevice, StorageUtils.GetDevGameRoot,
    "Resources/SpriteSheets/ui.png", "Resources/SpriteSheets/ui.xml");

// 或等分网格（无坐标文件，按 cellW×cellH 切格，名字 "r{行}_c{列}"）
var grid = GridAtlasSource.Load(GraphicsDevice, storage, "Resources/tiles.png", 32, 32);
```

> 图集是 `IDisposable`——在 `Shutdown` 里 `atlas.Dispose()`（会一并释放裁剪出的小纹理）。

### 加新格式 = 加一个 source 实现

要支持别的图集格式（TexturePacker JSON、Aseprite…），**新建一个 `.cs` 实现
`ISpriteAtlasSource` 即可**，核心与其它格式一行不用改：

```csharp
public sealed class MyJsonAtlasSource(string jsonPath) : ISpriteAtlasSource
{
    public IReadOnlyDictionary<string, RectInt> BuildRects(LocalStorage storage, int atlasW, int atlasH)
    {
        // 读 jsonPath，解析成 名字 -> RectInt(x, y, w, h)
    }
}
// 用：SpriteAtlas.Load(device, storage, png, new MyJsonAtlasSource(json));
```

### 绘制：整图 / 九宫格 / 三段条

`PaperSprite`（`Engine.Paper` 命名空间）给 Paper 加了几个扩展方法。**都要在元素
`Enter()` 之后调用**（画到当前元素矩形内），配合布局用：

```csharp
using Engine.Paper;   // DrawSprite / DrawNineSlice / DrawHBar

// 面板背景：九宫格。inset = 四边不拉伸的边框像素（角不变形、边单向拉、中心拉伸）
using (_paper.Column("Panel").Width(460).Height(300).Padding(34).Enter())
{
    _paper.DrawNineSlice(Gpu, atlas, "panel_brown", inset: 32);
    // ... 面板内容
}

// 按钮：同样九宫格，按下换 _pressed 子图
using (_paper.Box("Btn").Size(190, 54).OnClick(_ => {...}).Enter())
{
    _paper.DrawNineSlice(Gpu, atlas, pressed ? "button_pressed" : "button", inset: 14);
    _paper.Box("BtnText").Text("开始", _font).Alignment(PaperAlign.MiddleCenter);
}

// 图标 / 箭头：整张子图铺满
using (_paper.Box("Check").Size(24, 24).Enter())
    _paper.DrawSprite(Gpu, atlas, "iconCheck");

// 进度条 / 音量：三段式横条（Left 帽 + Mid 拉伸 + Right 帽），fill 是 0..1 填充比例
using (_paper.Box("Vol").Height(24).Enter())
{
    _paper.DrawHBar(Gpu, atlas, "barBack_left", "barBack_mid", "barBack_right", fill: 1f);
    _paper.DrawHBar(Gpu, atlas, "barYellow_left", "barYellow_mid", "barYellow_right", fill: _volume);
}
```

三个方法都有可选的 `alpha` 尾参，做整体淡入淡出时把过渡进度传进去（配合第 5 节）。

### 为什么用「裁剪」而不是直接采样子区域

Paper 的 `.Image()` 只能画**整张**纹理，没有 source-rect / UV 参数。`SpriteAtlas.Crop`
的做法是**把子图从图集拷成一张独立小纹理**（带缓存，跨帧只裁一次），九宫格的 9 块、
三段条的 3 段都各用一张小纹理走**整图绘制**路径——从而绕开图集 UV 矩阵的方向坑
（brush 矩阵的行/列约定很容易画歪，见第 8 节的已知坑）。

> `inset` 是**素材相关**参数：Kenney 面板 ~32、按钮 ~14，换一套素材要按其边框重调。
> 子图名字、inset、配色这些**绑具体图集**的东西留在业务层（如 `GameMenuSystem`），
> 引擎层的图集 / 绘制代码对它们无感知。

---

## 5. 动画（Animation）

Paper **自带动画系统**，不需要自己写计时器。核心是一组 `Animate*` 方法：每帧传入
**目标值**，它返回一个朝目标平滑推进的**当前值**，你把这个值接到位移 / 透明度 / 尺寸上即可。

| 方法 | 用途 | 关键参数 |
| --- | --- | --- |
| `AnimateBool(target, duration, easing, id)` | 0↔1 进度（最常用，做过渡） | `duration` 单程秒数、`easing` 缓动函数 |
| `AnimateFloat(target, speed, id)` | 追踪任意浮点值 | `speed` 越大越快（指数逼近） |
| `AnimateSpring(target, frequency, damping, id)` | 弹簧（带回弹） | `frequency` / `damping` |
| `AnimateColor(target, speed, id)` | 颜色渐变 | `speed` |
| `AnimateVec2(target, speed, id)` | 二维向量渐变 | `speed` |
| `AnimateAngle(targetDeg, speed, id)` | 角度（走最短弧） | `speed` |

缓动函数在 `Prowl.PaperUI.Easing` 静态类里，签名都是 `float→float`：
`Linear` / `EaseInOut` / `CubicInOut` / `QuartOut` / `SineInOut` / `ExpoOut` /
`BackOut`（回弹）/ `ElasticOut` / `BounceOut` / `SmoothStep` 等。

### ⚠️ 最重要的一条：必须在「稳定元素」内部调用

`AnimateBool` 等把动画状态**存在「当前元素」上**（`CurrentParent`），而 `EndFrame` 会
**清理本帧未创建的元素的存储**。这带来一个致命陷阱：

- 若在**没有进入任何元素**时调用（例如 `Update()` 一开头、还没 `Enter()` 任何容器），
  状态挂在每帧重建的根元素上，帧结束即被清掉 → 下一帧读回默认值 → **值永远不动、动画毫无效果**。

元素的内部 ID 由 `HashCode.Combine(父ID, stringID, ...)` 算出，**同一 string id + 同一父级
每帧得到相同 ID**，存储才跨帧稳定。所以规则是：

> **在一个每帧都用相同 string id 创建的容器 `Enter()` 之后，再调用 `Animate*`。**

推荐套路——放一个每帧都画的持久根容器，动画计算写在它内部：

```csharp
public void Update()
{
    // MenuRoot 每帧同 id => 同 ID => 动画存储跨帧保留。始终绘制以维持存储。
    using (_paper.Column("MenuRoot").Width(_paper.Stretch()).Height(_paper.Stretch()).Enter())
    {
        // ✅ 此处 CurrentParent = MenuRoot（稳定），AnimateBool 的状态才会持久
        float t = _paper.AnimateBool(_open, duration: 0.18f, Easing.CubicInOut, id: "PanelT");

        // 用 t 驱动位移 + 透明度（见下）
        DrawPanel(t);
    }
}
```

> `id` 建议显式传一个稳定字符串；不传时它用 `[CallerLineNumber]` 当 key，重构挪行会丢状态。

### 把动画值接到视觉上

拿到 `0..1` 的进度后，常见三种落地方式：

```csharp
// 1) 横向滑动：TranslateX（像素位移，不影响布局）
.TranslateX(offsetX)

// 2) 淡入淡出：Paper 没有整体 Opacity，靠把 alpha 乘进颜色。
//    把颜色助手 C() 改成实例方法、统一乘一个 _uiAlpha 乘子即可让整块一起淡：
private float _uiAlpha = 1f;
private PaperColor C(byte r, byte g, byte b, byte a = 255) => new(r, g, b, (byte)(a * _uiAlpha));

// 3) 尺寸/圆角等：直接把插值结果传进对应链式方法
.Height(baseH + t * 40)
```

### 完整示例：滑动 + 淡入淡出的页面过渡

`Tests/Game0/ParperUITest.cs` 的 `GameMenuSystem` 用 `AnimateBool` 做了「主菜单 ↔ 设置」
的**横向滑动 + 淡入淡出**过渡。要点：

- 顺序式过渡：切页时不立即换内容，先让旧页 `TranslateX` 滑走并淡出（进度 `0→1`），
  到位后交换页面，再让新页从反方向滑入淡入（`1→0`）。
- 遮罩用**独立的 `AnimateBool`**（单独 id），只在整菜单开 / 关时淡，换页时保持常亮，
  避免中途闪一下。
- 面板整体淡入淡出靠上面的 `_uiAlpha` 乘子路由所有颜色。

排障：动画「没效果」时，先打印 `Animate*` 的返回值和 `_paper.DeltaTime`——
若返回值在 0/1 之间不变、或 `DeltaTime==0`，多半就是**没在稳定元素内部调用**（状态被清）
或没正确 `BeginFrame(Time.Delta)`。

---

## 6. 与 ImGui / 世界渲染共存

本工程 UI 分层，绘制顺序决定叠放：

1. `Window.Clear(...)`
2. 世界 / Batcher（最底层）
3. ImGui（`_imGui.Render()`）
4. **Paper（`_paper.EndFrame()`，最上层）**

键盘输入的归属：ImGui 和 Paper 都可能想要文本输入，判断时两者都要考虑：

```csharp
if (_imGui.WantsTextInput || _paper.WantsCaptureKeyboard)
    Window.StartTextInput();
else
    Window.StopTextInput();
```

---

## 7. 在 ECS 里用 Paper

把 `Paper` 和 `FontFile`（以及需要的纹理）注入 pipeline，在 `IUpdateSystem` 里声明 UI
（因为 UI 声明属于 `BeginFrame`/`EndFrame` 之间，走 `Update` 阶段）：

```csharp
_pipeline = EcsPipeline.New()
    .Inject(_paper)
    .Inject(_paperFont)
    .Inject(_paperTexture)
    .AddModule(new MyUiModule())
    .AutoInject()
    .BuildAndInit();

public class MyUiSystem : IUpdateSystem
{
    [DI] private Paper _paper = null!;
    [DI] private FontFile _font = null!;

    public void Update()
    {
        using (_paper.Column("Panel").Padding(16).Enter())
        {
            _paper.Box("Hello").Text("Hello Paper", _font).FontSize(18);
        }
    }
}
```

完整的综合示例见 `Tests/Game0/ParperUITest.cs` 里的 `GameMenuSystem`（游戏菜单：主菜单 /
设置页、分辨率切换、可拖拽音量滑块、全屏开关，以及页面间的滑动淡入淡出过渡）。

---

## 8. 渲染原理（排障时看）

`FosterCanvasRenderer` 把 Quill 的绘制命令翻译成 Foster draw call，shader
（`QuillCanvas.hlsl`）按 UV 分三条路径：

- **纯色 / 矢量**：无纹理时绑定 1×1 白纹理，`color × white = color`。
- **图片 brush**：**不走逐顶点 UV**，而是用 `BrushTextureMatrix × 片元屏幕坐标`
  反算纹理 UV。
- **SDF 文字**：Quill 把文字 UV 加了 `+2` 偏移作标记，shader 据此走单通道 SDF 解码。

> ⚠️ **已知坑（已修复）**：Prowl 的 `Brush.TextureMatrix` 是**列向量约定**
> （平移在第 4 列），而本管线走「C# 行主序上传 → HLSL 列主序读取」隐含一次转置。
> 投影矩阵恰好抵消正确，但这个 brush 矩阵会被转错，导致图片 UV 丢失平移、被挤到左上角。
> 修复是在上传前显式再转置一次：
> `BrushTextureMatrix = Matrix4x4.Transpose(call.Brush.TextureMatrix)`。
>
> 排障提示：纯色 UI 正常**说明不了**图片路径对不对（纯色不经过 brush 矩阵）。
> 图片画歪时，在 `RenderCalls` 里 dump 真实矩阵和顶点 UV 即可定位。

---

## 速查清单

- [ ] 构造：`FosterCanvasRenderer` → `Paper` → `FontFile`
- [ ] `Update`：`PaperInput.Update` → `BeginFrame` → 声明 UI（每帧）
- [ ] `Render`：`EndFrame`
- [ ] 窗口尺寸变化：`SetResolution`
- [ ] 分辨率 / dpiScale / input scale 三者一致（本工程都用像素 + 1f）
- [ ] 文本输入：`WantsCaptureKeyboard`
- [ ] 容器用 `using (...Enter())`，叶子直接链式收尾
- [ ] 动画：`Animate*` 必须在**稳定 string id 的容器内部**调用，否则状态被清、无效果
- [ ] `Shutdown`：`_paperRenderer.Dispose()` + 自建纹理各自 Dispose
