# DragonLib 新项目创建提示词

本文件是 DragonLib 的新游戏项目创建指南。它约定使用 DragonLib 时的工程目录、Paper UI 与 Origami 控件、数据与逻辑分层、C# 风格和中文注释方式，不依赖其他游戏仓库。这里的游戏目录是推荐模板，DragonLib 本身并不强制应用采用这些目录名。

本文件放在 DragonLib 根目录。可以直接让 AI 阅读本文件并附上项目需求，也可以填写下方模板后复制“提示词开始”到“提示词结束”之间的内容。复制到其他位置时，应明确提供 DragonLib 的实际路径；文末链接均相对 DragonLib 根目录。

例如：“阅读 DragonLib 根目录的 New-project-prompt.md，按其中约定在同级目录创建 MyGame。玩法是……，第一版需要……。”

## 提示词开始

请直接在指定目录创建一个可编译、可运行、便于继续开发的游戏项目，遵守以下约定。先检查工作目录和依赖，再完成最小玩法闭环、UI 和必要验证；不要只给方案或一批无法运行的代码片段。

### 项目需求

```text
项目名称：<填写英文工程名>
创建位置：<填写新项目的绝对路径>
DragonLib 位置：<本文件所在的 DragonLib 根目录；复制提示词时填写实际路径>
游戏类型与视角：<例如俯视角经营、动作、解谜>
核心玩法：<玩家做什么，如何获得反馈，如何完成一局>
第一版必须完成：<列出 3～5 个可以实际操作和验证的功能>
视觉风格：<填写；未指定时采用简洁的 2D 图形和与世界协调的 HUD>
目标平台：<默认 Windows x64 和 Web；若只需其中一种，请明确填写>
是否需要独立编辑器：<默认暂不需要；需要时说明编辑哪些内容>
语言：<默认游戏 UI 支持中文和英文，代码标识符用英文，注释用中文>
明确不做的内容：<可留空>
```

如果项目名称、创建位置或核心玩法缺失且无法从对话中确定，集中询问这些必要信息。其余可逆的实现细节自行选择，简要说明假设后继续。已有文件和用户改动应当保留。

### 技术栈和依赖

1. 使用 C#、DragonLib 和 Foster。当前默认 .NET 10，桌面项目以 `net10.0` 为目标框架，开启 `Nullable` 和 `ImplicitUsings`。创建前核对本地 Engine 的目标框架；库版本变更时保持匹配。
2. 使用 DragonLib 已有的 `Engine`、Foster 游戏循环、输入、渲染与资源能力。先检查实际 `.csproj`、源码和示例中的 API，再写调用代码；不要凭印象编造库的接口。
3. 首先确定 DragonLib 根目录，再检查其中的 `Libs/Engine/Engine.csproj` 和 `Libs/DragonLib.Platform.props`。新游戏默认建在 DragonLib 的同级独立目录，通过 `ProjectReference` 引用库；不要因为当前工作目录在 DragonLib 就把游戏业务写进库。路径相对各 `.csproj` 或 props 文件计算，不相对终端当前目录计算。
4. UI 使用 `Prowl.PaperUI`，通用控件优先直接使用配套的 **Origami**（命名空间 `Prowl.OrigamiUI`）。Engine 已通过本地项目引用接入 Origami，无需另装 NuGet 包。底层通过 `Engine.Paper.FosterCanvasRenderer` 绘制、`Engine.Paper.PaperInput` 接入输入，字体使用 `Prowl.Scribe.FontFile`。沿用 DragonLib 维护的依赖，不另装一套同名上游库。
5. 游戏资源和用户设置通过 DragonLib 的存储能力接入。Authoring 保持独立，以委托或轻量适配接口接入资源读写，不直接依赖图形框架。
6. 默认采用普通 Engine 与显式状态及逻辑系统，不强制使用 ECS。只有需求适合或用户指定时才额外引用 `Libs/Engine.ECS/Engine.ECS.csproj`，按其实际 Pipeline 和组件 API 实现，不重复建立两套权威实体状态。
7. 游戏 UI 使用 Paper。需要 Dear ImGui 调试工具时才引用 `Libs/Engine.Editor/Engine.Editor.csproj`；它是桌面工具依赖。Engine.ECS 当前也依赖 Engine.Editor，因此选择 ECS 时须核对目标平台及传递依赖，不能直接假定可发布 Web。
8. 物理、音频、Web、多线程、自定义 Shader 等按实际需求添加。优先复用 Engine 已提供的能力，不因库中存在某个模块就全部接入。
9. 如果依赖缺失，说明缺少的具体路径和受影响的步骤，继续完成不依赖它的工作；不要创建同名空壳冒充真实库，也不要声称已构建成功。

### 目录结构

先区分库目录和新游戏目录：

```text
<Workspace>/
├─ DragonLib/                    引擎库，不放新游戏业务代码
│  ├─ New-project-prompt.md       本文件
│  ├─ Libs/
│  └─ Tools/
└─ <ProjectName>/                新建的独立游戏项目
```

默认布局下，游戏根目录的 `Directory.Build.props` 导入 `../DragonLib/Libs/DragonLib.Platform.props`，`Scripts/Core/Core.csproj` 引用 `../../../DragonLib/Libs/Engine/Engine.csproj`。若位置不同，按真实布局调整。不要复制 DragonLib 源码进游戏，也不要直接把 DragonLib 的整个 `Directory.Build.props` 作为游戏配置导入。

采用以下结构。`Editor`、`Game.Web`、`Shaders` 和各层子目录按功能需要创建，不预先堆放空文件或占位实现。

```text
<ProjectName>/
├─ <ProjectName>.slnx
├─ Directory.Build.props         集中配置构建输出及平台属性
├─ .gitignore
├─ AGENTS.md                     简短、可执行的项目开发约定
├─ README.md                     启动、操作、依赖与开发入口
├─ play.cmd                      常用游戏启动入口
├─ edit-world.cmd                可选，独立编辑器启动入口
├─ publish.cmd                   Windows Release 发布入口
├─ publish_web.bat               Web 发布入口，支持 aot 和 serve
├─ Scripts/
│  ├─ Authoring/                  配置定义、校验、序列化、场景文件
│  │  └─ Authoring.csproj
│  ├─ Core/
│  │  ├─ Core.csproj              共用游戏库，不产出 exe
│  │  └─ Content/
│  │     ├─ README.md             分层职责和模块入口
│  │     ├─ Definitions/          枚举、固定映射、共享类型
│  │     ├─ Runtime/
│  │     │  ├─ Entities/          实体存储及属性列
│  │     │  ├─ State/             对局进度和权威状态
│  │     │  └─ World/             世界数据、占位和可见性数据
│  │     ├─ Logic/
│  │     │  ├─ <Feature>/         按实际玩法拆分规则模块
│  │     │  └─ Queries/           可重建的查询索引与算法缓存
│  │     ├─ Integration/
│  │     │  ├─ GameHost.cs        组装、启动、重开和游戏循环
│  │     │  ├─ Systems/           输入、时间和规则调用的适配
│  │     │  └─ Diagnostics/       调试命令和诊断入口
│  │     └─ Presentation/
│  │        ├─ UI/                HUD、菜单、主题和布局
│  │        ├─ Rendering/         世界绘制、特效和渲染缓存
│  │        ├─ Map/               可选，只读地图视图和显示缓存
│  │        └─ Audio/             可选，音效和音乐播放
│  ├─ Game/
│  │  ├─ Game.csproj              桌面玩家入口，引用 Core
│  │  └─ Program.cs
│  ├─ Editor/                     可选，与 Game 并列的工具宿主
│  │  ├─ Editor.csproj            引用 Core
│  │  ├─ Documents/              编辑草稿、撤销重做、脏标记
│  │  ├─ Integration/            编辑与试玩生命周期
│  │  └─ Presentation/           工具面板和编辑视图
│  └─ Game.Web/                   Web 入口，复用 Core；仅桌面项目可省略
├─ Resources/
│  ├─ Balance/                    数值与内容目录 JSON
│  ├─ Worlds/                     场景或关卡文件
│  ├─ Locale/                     zh.json 和 en.json
│  ├─ Fonts/                      字体及许可文件
│  ├─ Textures/                   按需添加贴图
│  └─ Audio/                      按需添加音频
├─ Shaders/                       可选，Shader 源码
│  └─ Compiled/                   运行所需的预编译产物
├─ tests/
│  ├─ ArchitectureChecks/         层间依赖和命名空间检查
│  ├─ <Feature>Checks/            无窗口的规则验证
│  ├─ <Feature>Smoke/             需要图形环境的画面与输入验证
│  └─ README.md                   每项检查的用途和运行方式
├─ tools/                         构建、资源处理和开发工具
├─ Doc/
│  ├─ README.md                   文档索引
│  ├─ Gameplay/                  当前玩法说明
│  ├─ Design/                    内容设计、调参和扩展指南
│  ├─ Tech/                      当前技术实现与约束
│  ├─ Records/                   带日期的测试或决策记录
│  └─ Archive/                   已被替代的历史方案
└─ artifacts/                     构建、截图、日志等可再生成产物
   ├─ build/                     常规构建中间文件与输出
   ├─ build-aot/                 Web AOT 独立构建目录
   └─ publish/
      ├─ win-x64/                桌面可分发目录
      ├─ web/wwwroot/            Web 默认发布的完整站点
      └─ web-aot/wwwroot/        Web AOT 发布的完整站点
```

`GameHost`、`<Feature>` 等名称是模板，需要替换成准确的职责名称。玩法子目录按新项目的实际需求建立；不预设塔防、经营、战斗或关卡编辑等特定玩法。

构建产物集中在 `artifacts/build`；其他临时产物放在 `artifacts` 的对应子目录。将 `artifacts`、`bin`、`obj`、IDE 缓存和临时文件加入忽略规则。资源和运行所需的预编译 Shader 不能被当作普通构建缓存删除。

### 桌面与 Web 发布入口

发布脚本是项目交付的一部分，与 `play.cmd` 一起放在游戏根目录。默认同时提供 `publish.cmd` 和 `publish_web.bat`；用户明确只要一个平台时才省略另一个入口及其工程。不创建指向不存在项目的空脚本。

两个脚本都必须从自身所在目录运行，使用 `cd /d "%~dp0"` 或等效实现，不能写死开发者的绝对路径；所有可能含空格的路径加引号。发布失败返回非零退出码，只有成功后才显示完成消息和产物位置。发布表示生成分发包，不自动上传或部署。

**桌面发布 `publish.cmd`**

- 发布 `Scripts/Game/Game.csproj`，默认 `Release`、`win-x64`，产物写到 `artifacts/publish/win-x64`。
- 默认生成自包含的玩家程序，明确设置 `SelfContained`；如采用单文件 exe，再配置 `PublishSingleFile` 和原生库提取。单文件 exe 不等于没有外部资源，所需 `Resources`、字体、音频和许可证仍需随包分发。
- 在项目中配置 `CopyToPublishDirectory`，发布资源不依赖开发机目录。运行时以发布程序所在目录读取随包资源，可通过 `GameStorage.UseResources(StorageUtils.GetReleaseGameRoot)` 接入。
- 涉及反射配置和序列化时，默认不要随意开启桌面裁剪；需要裁剪时先验证真实加载流程。
- 分发的是整个发布目录。脚本或 README 明确压缩包范围，避免只发送 exe 后丢失资源。

核心发布命令为：

```powershell
dotnet publish Scripts/Game/Game.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

**Web 发布 `publish_web.bat`**

- 发布 `Scripts/Game.Web/Game.Web.csproj`，使用 `Microsoft.NET.Sdk.WebAssembly`、`net10.0-browser`，引用游戏 Core，并导入 DragonLib 的 `Libs/DragonLib.Web.targets`。
- Core 为桌面和浏览器分别编译；资源通过 `WebResource` 及正确的 `Prefix` 声明，由 targets 生成 `assets.json` 并预加载到 title storage。资源路径与游戏调用 `GameStorage.Resources` 时使用的路径一致。
- 无参数时生成默认 Web 包，输出 `artifacts/publish/web/wwwroot`；`aot` 参数启用 `RunAOTCompilation=true`，输出 `artifacts/publish/web-aot/wwwroot`，同时将 `ArtifactsPath` 指向独立的 `artifacts/build-aot`，避免复用另一种构建模式的原生中间文件。
- AOT 裁剪及反射 JSON 设置优先沿用 `DragonLib.Web.targets`，不要用桌面的发布配置覆盖它。
- `serve` 参数在成功发布后启动本地预览。可调用 DragonLib 的 `Libs/Foster.Web/serve.py`，将本次输出的 `wwwroot` 传给 `--root`；支持与 `aot` 组合使用。仅在需要预览时检查 Python。
- 检查 `wasm-tools` 工作负载；缺失时给出 `dotnet workload install wasm-tools` 指引及明确错误。发布后检查站点入口、资源清单和运行文件，不能只看命令是否退出。
- 使用 HTTP(S) 预览，`.wasm` 以 `application/wasm` 返回；不能通过双击 `index.html` 验证。部署整个 `wwwroot`，包括运行时、资源清单与所有资源；浏览器音频需验证用户交互后的解锁。
- 避免旧的哈希文件和已移除资源混入新包。需要清理旧产物时，先确认绝对目标路径位于当前游戏的 `artifacts/publish` 内，再删除相应生成目录，不能清理源码或 DragonLib。

在 README 中说明以下入口、前置依赖和对应输出：

```powershell
.\publish.cmd
.\publish_web.bat
.\publish_web.bat aot
.\publish_web.bat serve
.\publish_web.bat aot serve
```

### 分层和依赖方向

先按职责确定文件所在层，再在层内按玩法划分目录。命名空间与目录一致，例如 `Core.Content.Logic.Combat`；不要建立一个收纳全部代码的玩法目录，也不要用全局 using 或转发类掩盖错误依赖。

| 模块 | 负责什么 | 依赖限制 |
| --- | --- | --- |
| Authoring | 配置类型、默认值、校验、读写、版本迁移和场景生成 | 不引用 Core、Game、Editor 或图形设备 |
| Definitions | 共享枚举、固定类型和映射 | 可引用 Authoring，不引用 Runtime、Logic 或客户端层 |
| Runtime | 本局权威数据、实体存储、数组扩容、句柄和版本号 | 可引用 Authoring、Definitions 和必要的 Engine 基础存储，不引用 Logic、Integration、Presentation |
| Logic | 状态转换、玩法规则、导航和可重建查询缓存 | 读取配置与 Runtime；不引用游戏宿主、输入、窗口、Paper、图形或音频设备 |
| Integration | 读取输入和时间、生命周期、系统组装与执行顺序 | 调用 Logic，组装 Presentation；不实现伤害、收入等玩法公式 |
| Presentation | UI、绘制、动画、音频和显示缓存 | 读取状态、形成操作请求；不另存一份权威玩法状态，不在绘制中推进模拟 |

`Game` 和可选的 `Editor` 单向引用 `Core`；`Core` 引用 `Authoring` 和 DragonLib 的必要项目。Game、Core、Authoring 不得反向引用游戏自己的 Editor 程序集。这里的 `Scripts/Editor` 是新游戏的工具宿主，与 DragonLib 的 `Engine.Editor` 库是两个不同概念。

Integration 和 Presentation 同属客户端层，宿主可以提供它们所需的设备和视窗。优先显式传入依赖；没有实际复用需求时，不为每个类增加接口、服务容器或独立程序集。Core 内的分层先通过目录和检查约束。

### 状态和规则如何写

- `Runtime` 保存真正的对局进度，例如生命、资源、冷却、任务和已探索区域。允许维护存储一致性，不负责决定攻击目标、收入或放置是否合法。
- `Logic` 接收状态、操作参数和明确的 `dt`，能够在没有窗口时运行。不要在里面读取全局键盘、帧时钟或游戏宿主。
- 默认以固定 60 Hz 推进模拟，暂停和倍速由 Integration 调度。UI 动画时间与模拟时间按用途区分。固定步长并不自动保证完全确定性，需要重放时还要控制随机种子与迭代顺序。
- UI 点击形成操作请求，经适配层调用 Logic。合法性、费用扣除和结果修改集中在 Logic；禁用按钮只是反馈，不能替代规则校验。
- 默认的非 ECS 方案中，大量同类实体采用 Engine 的 SlotMap 和游戏自己定义的 SoA 属性列：属性数组按实体槽位对齐，跨帧引用使用有代际信息的句柄。不要为每个单位创建带独立 `Update()` 的对象树。少量全局状态可以使用普通类或结构体。若选择 DragonECS，则使用其组件存储和句柄约定，同时保留数据、规则和表现的职责划分。
- 新增实体字段时同步检查分配、初始化、扩容、移除、槽位复用和重开，避免旧数据留给新实体。
- 配置中的静态类型信息与实例状态分开保存。价格、攻击方式等由目录定义；实例只保存类型引用和自身变化的数据。根据能力编写规则，不在多处按具体内容 ID 堆分支。
- 数值从已校验的配置快照读取，不在每帧解析 JSON。默认值、全局配置和关卡覆盖的优先级应明确，UI 和规则使用同一份生效数值。
- 空间索引、路径和统计缓存放在 Logic；渲染、图标和地图显示缓存放在 Presentation。每个缓存注明输入、失效条件、重建时机和释放责任，不以缓存替代权威状态。
- 使用事件或版本号使缓存失效。被观察的数据通过统一入口修改；事件回调只记录变化，避免重入修改模拟。重开、切换世界和退出时解除旧订阅并释放资源。
- 先保证正确性，再依据测量优化热点。热循环避免重复全表扫描、每帧临时集合和无谓分配；空间索引、Chunk、对象池与并行仅在规模需要时引入。
- 引入并行时，工作线程读取稳定数据并写各自结果区间，再由主线程有序提交。不要让工作线程直接修改实体存储或调用 GPU。

### UI 用什么以及如何接入

使用 **Paper UI + Origami 控件库**。Paper 负责即时模式布局、输入和绘制组织，Origami 提供可直接调用的通用控件。运行中每帧重新声明 UI，控件使用稳定的标识；动态列表优先使用稳定内容 ID，避免插入、删除或排序后焦点跳到其他条目。

**先查 Origami 是否已有对应控件，再决定是否自定义。** 主菜单、设置和编辑器表单默认复用现成控件，不从 `paper.Box` 开始重写按钮、文本编辑、滑动、弹出选择或焦点行为。特殊的世界 HUD、异形控件和游戏图标才按需要使用 Paper 或自定义绘制；仅需改变外观时，优先使用主题、控件样式和自定义绘制入口，保留既有交互。

| 界面需求 | 优先查看的 Origami 控件 |
| --- | --- |
| 按钮与开关 | Button、IconButton、Toggle、Switch、Checkbox、Radio |
| 文字、数字和范围输入 | TextField、NumericField、Slider、RangeSlider、VectorField |
| 选项与页面切换 | Dropdown、MultiDropdown、RadioGroup、Tabs |
| 列表与编辑器数据 | ScrollView、Table、Tree、PropertyGrid |
| 菜单与反馈 | MenuBar、ContextMenu、Modal、Tooltip、Toasts、ProgressBar |

使用前读取 `Libs/ThirdParty/Prowl/Origami/Origami.cs` 和对应的 `Widgets/*.cs`，以本地签名为准。例如按钮从 `Origami.Button(...)` 创建，文本框从 `Origami.TextField(...)` 创建；这类 builder 配置完成后调用 `.Show()`。不要照搬其他版本的参数形式，也不要假定所有容器都采用相同的结束方式。

主题集中在游戏的 `GameTheme` 中：通过 `OrigamiTheme.CreateDefaults()` 建立主题，配置游戏字体、颜色和尺寸，再用 `Origami.SetTheme(...)` 应用。游戏与编辑器共用进程且样式不同时，用 `Origami.PushTheme(...)` 的作用域隔离；启用主题渐变切换时每帧调用 `Origami.TickTransition(dt)`。无需重写一套通用控件库。

统一创建和管理 `Paper`、`FosterCanvasRenderer`、字体及公用 UI 纹理，Origami 使用同一个 Paper 实例和同一帧流程。接入前阅读 DragonLib 根目录下的 `Libs/Engine/Paper/README.md`，再核对 `PaperInput.cs` 和渲染器源码。游戏自己的主题配置、HudLayout 和 GameHost 在新项目实现，基础控件复用 Origami。

帧流程是：输入转发 → `BeginFrame` → 构建 UI → 绘制世界 → `EndFrame` 叠加 UI。每次开始与结束必须配对；固定步进可能在两次渲染之间发生多次更新，宿主须明确处理尚未结束的 Paper 帧，验证一次点击只执行一次，按下与松开不会丢失。不要在每个加速模拟子步中重复处理同一个 UI 操作。

默认使用统一的 UI 逻辑坐标。DragonLib 的接入示例使用像素坐标加 `1f` 缩放，也可以作为第一版起点；一旦增加缩放，以下参数必须一起调整：

- 一个 `UiScale` 负责物理像素与 UI 坐标的转换。
- `SetResolution` 使用 UI 逻辑宽高，`BeginFrame` 的 DPI 参数和 `PaperInput.Update` 的缩放参数与之保持一致。
- 绘制、点击判定、悬停、相机预留区域和测试点击位置使用同一套布局结果。
- 处理窗口变化、文字输入焦点、滚轮、模态窗口和 UI 输入捕获。点击按钮、输入文字或在面板上滚动时，不应同时触发世界操作。

在 `Presentation/UI` 中拆出以下职责，名称可随项目调整：

| 文件 | 职责 |
| --- | --- |
| `GameTheme.cs` | Origami 主题配置，以及自定义 HUD 共用的颜色与字体样式 |
| `HudLayout.cs` | 布局位置、尺寸、锚点和窄屏策略 |
| `GameHud.cs` | 对局信息、交互入口、悬停提示 |
| `GameMenu.cs` | 主菜单、暂停、设置和结算 |
| `GameUiArt.cs` | 需要时生成或缓存图标与柔边纹理 |

不要在每个控件内分别写颜色、尺寸和命中区域，也不要为每帧重新加载字体或生成相同纹理。

### UI 视觉和文字约定

1. 对局 HUD 以世界画面为主，常驻内容保持少量数字、图标和短句。详细解释放入悬停提示或按需展开区域；文字在复杂背景上用投影和柔和底衬保证可读。
2. 控件图标与游戏美术一致。主题未指定时采用简洁、克制的表现；颜色、按钮形状、装饰和反馈根据新游戏题材确定，不把某一种游戏美术当成库的固定主题。
3. 对局 HUD、模态菜单和编辑器的需求不同。设置、结算以及编辑器表单允许使用清晰的面板、边框、输入框和滚动区域，不把 HUD 的低遮挡要求强套给工具界面。
4. 当前 `FosterCanvasRenderer` 的画布路径支持纯色、图片和字体绘制，不应假定 Paper 声明的所有画刷都受支持。默认不要直接使用 `BackgroundLinearGradient`、`BackgroundRadialGradient`、`BackgroundBoxGradient`、`BoxShadow`；柔光、渐变和阴影通过预先烘焙的纹理与 `Image(texture, tint)` 实现。Origami 也受同一后端限制：接入时默认将 `Origami.DropShadowsEnabled` 和 `Origami.GlowsEnabled` 设为 `false`，并验证所用控件的弹出、焦点和选中效果；这两个开关不代表所有其他效果都已兼容。库更新后若要使用这些能力，先以实际后端与真实画面确认支持情况。
5. 字体是游戏资源，需要在新项目的 `Resources/Fonts` 中准备并保留许可，不假定 DragonLib 已提供某个游戏字体。中文字体必须包含所需字形；数字装饰字体只用于其支持的字符，工具界面可用等宽字体。注册回退字体后验证中文、标点和不同字重，不能假定不同样式一定能正常回退。
6. 玩家可见文字从 `Resources/Locale/zh.json` 和 `en.json` 按键读取，使用完整句子和格式占位符；不要在代码中拼接翻译片段。内容名称与描述通过统一的本地化入口读取。
7. 检查默认窗口、窄窗口、缩放、中英文长文本、悬停和禁用状态。英文变长时应换行、适配或调整布局，不能仅验证中文截图。

### C# 编码风格

- 使用文件范围命名空间，4 空格缩进，大括号另起一行。一个文件围绕一个主要职责组织；密切相关的小类型可以放在一起。
- 类型、方法、属性和公开成员用 `PascalCase`，参数与局部变量用 `camelCase`。新项目的私有字段统一用 `_camelCase`；接入已有文件时沿用其局部风格，避免仅为格式批量改名。
- 使用明确的职责名称，如 `CombatLogic`、`UnitData`、`WorldRenderer`，不建立无边界的 `Manager`、`Utils` 或 `Common` 大杂烩。
- 配置快照适合用 `record` 和 `init`，简单值类型适合用 `readonly record struct`。有实际需要才使用继承、泛型或接口；只有生命周期明确且不需要继承的类才封为 `sealed`。
- 类型明显时可用 `var`；集合表达式、模式匹配、主构造函数等语言特性以便于阅读为前提。不要把复杂状态变更压成难以调试的一行。
- 优先用早返回减少嵌套。按业务动作拆方法，保持更新顺序和副作用清楚；避免在属性 getter 中推进玩法或执行磁盘操作。
- 配置和文件属于输入边界，需要校验并返回可定位的错误。异常消息说明具体文件、字段或操作，不写空 `catch`，不以静默默认值掩盖损坏数据。
- 业务拒绝可返回明确的结果和原因；不要用异常表示每帧常见的“不能放置”或“金币不足”。
- 文件、订阅、纹理和其他 GPU 资源有明确所有者，并在适当生命周期释放。避免为方便访问而散布可变静态状态。

### 中文注释怎么写

注释帮助下一位开发者理解职责、约束和原因。不要逐行翻译代码，不需要给每个自解释的字段、getter 或局部变量补注释。

**类型和关键方法**使用 `/// <summary>`，说明负责什么以及必要的边界。参数有单位、范围、坐标空间或特殊约定时写 `<param>`；返回值有特殊含义时写 `<returns>`。引用类型或成员时可用 `<see cref="..."/>`，便于随重命名维护。

**状态字段**重点解释单位、哨兵值、有效期和槽位对应关系，例如“秒”“世界格坐标”“无目标时为无效句柄”。不要只重复字段名。

**实现内部**使用 `//` 解释“为什么这样做”：算法前提、排序要求、缓存失效、线程边界、避免的错误或平台限制。修改行为时同步更新注释，不保留描述旧逻辑的文字。

下面是注释与分层的简化示例，用来说明写法，不要求新项目一定包含周期收入玩法。

```csharp
// 文件：Scripts/Core/Content/Runtime/State/IncomeState.cs
namespace Core.Content.Runtime.State;

/// <summary>保存周期收入的余额与累计进度，不负责计时推进和结算规则。</summary>
public sealed class IncomeState
{
    public int Coins;

    /// <summary>距上次结算累计的模拟秒数；暂停不增加，重开时清零。</summary>
    public float ElapsedSeconds;
}
```

```csharp
// 文件：Scripts/Core/Content/Logic/Economy/IncomeLogic.cs
using Core.Content.Runtime.State;

namespace Core.Content.Logic.Economy;

/// <summary>按显式传入的模拟时间结算收入，不读取窗口或 UI 状态。</summary>
public static class IncomeLogic
{
    /// <summary>推进计时并结算完整周期，剩余时间留待下次调用。</summary>
    /// <param name="state">本局状态，由宿主在重开时重置。</param>
    /// <param name="dt">模拟秒数，调用方保证为有限的非负值。</param>
    /// <param name="intervalSeconds">已校验配置中的周期秒数，必须有限且大于零。</param>
    /// <param name="coinsPerCycle">每周期收入，来自已校验的配置。</param>
    public static void Step(IncomeState state, float dt,
        float intervalSeconds, int coinsPerCycle)
    {
        state.ElapsedSeconds += dt;

        // 一次推进可能跨过多个周期；逐次扣除保留余量，避免漏结算或计时漂移。
        while (state.ElapsedSeconds >= intervalSeconds)
        {
            state.ElapsedSeconds -= intervalSeconds;
            state.Coins += coinsPerCycle;
        }
    }
}
```

其他适合写注释的地方：

```csharp
// 建筑位置改变会使空间索引失效，必须经统一入口更新版本号。
// 工作线程只记录候选结果；实体删除和伤害结算在主线程按槽位顺序提交。
// UI 使用逻辑坐标，命中测试前先把鼠标的物理像素除以 UiScale。
```

避免 `// 增加计时器`、`// 遍历列表` 这类复述代码的注释。临时方案需要写明原因和移除条件；不要留下没有范围和行动说明的 `TODO: 优化`。设计推导、长篇流程和使用教程放在 `Doc`，源码只保留就地需要的信息。

### 创建与验证流程

1. 阅读目标目录已有的 `AGENTS.md`、README 和工程配置，检查 Git 状态及 DragonLib 位置。先读 DragonLib 的 `Libs/Engine/README.md`、`Libs/Engine/Paper/README.md` 和需要使用的 Origami 控件源码；已有代码优先复用，遇到依赖 API 不确定时先查源码。若同级存在 DragonLib.Tests，可按需查示例，但它不是创建新游戏的必需依赖。
2. 建立解决方案、Authoring、Core、Game 和最小资源加载，先让空窗口、中文文本和一个可交互的 Origami 控件正常运行。
3. 完成一个贯穿各层的功能：输入 → 操作请求 → Logic 校验与状态变更 → 画面反馈。再补完需求中约定的第一版玩法闭环和重开。
4. 需要编辑器时，再增加 Editor 的文档草稿、保存和试玩；每次试玩使用独立快照，避免对局修改污染编辑文档。游戏发布包不得携带对 Editor 程序集的依赖。
5. 按目标平台增加 `Game.Web` 和 Core 的浏览器目标，依据本地 DragonLib 的平台配置与 Web targets 接入，复用核心玩法。核对资源预加载、用户存储、音频解锁及静态服务启动方式，不直接复制桌面文件系统假设。
6. 实现根目录的 `publish.cmd` 和 `publish_web.bat`，按前述发布约定验证所选平台的实际产物；不能以开发模式能启动代替发布包可运行。
7. 编写与当前功能相关的检查。默认使用可通过 `dotnet run --project` 执行的检查项目，失败时返回非零退出码；没有需要时不引入额外测试框架。
8. 完成 README、Content 分层说明和简短 AGENTS.md。README 写明启动和发布脚本用法、输出目录及依赖；新文档加入 `Doc/README.md`，说明真实实现与尚未实现的内容。

至少验证以下内容：

- 构建成功，游戏能够启动，默认场景可以操作并重开。
- 规则可无窗口运行，覆盖正常路径及关键拒绝条件；涉及缓存或句柄时验证失效和复用。
- 架构检查覆盖命名空间与目录匹配、禁止的层间引用，以及 Game/Editor 边界。检查实际符号，避免仅用字符串搜索把注释误判为引用。
- UI 的文字、按钮和输入命中正确，点击不会穿透；窗口缩放和语言切换不会导致明显溢出。
- 配置出错能定位原因；涉及保存时，写入和重新读取的内容一致。
- 桌面发布包可从发布目录启动，资源和原生依赖齐全；Web 默认包及 AOT 包可通过本地 HTTP 服务打开并完成基本交互。仅验证目标平台，环境缺失的发布模式如实标为未验证。

仅运行已创建且与本次改动相关的检查。画面检查需要真实图形环境，保存截图后实际查看；不能用“构建成功”代替视觉验收。性能测试使用 Release，并报告场景规模和测量条件。

完成对应项目后，从新仓库根目录运行，例如：

```powershell
dotnet build <ProjectName>.slnx
dotnet run --project Scripts/Game
dotnet run --project tests/ArchitectureChecks
dotnet run --project tests/<Feature>Checks
dotnet run --project tests/<Feature>Smoke
```

上面的占位名称需要替换为实际项目。不要报告不存在的命令，也不要把 DragonLib.Tests 的测试或示例当作新游戏已经实现的功能和检查。

### 最终交付

交付可运行的项目文件及对应平台的发布脚本，并简要说明：实现了哪些玩法、代码主要放在哪里、如何启动和发布、产物保存在哪里、实际执行了哪些验证、还有哪些明确限制。没有运行的检查或缺少环境的步骤应如实列出。

遇到实现失败时修复到可验证状态；无法完成的部分给出具体原因。不要把空方法、固定假数据或尚未连接的按钮描述为已经完成的功能。

## 提示词结束

## DragonLib 内的参考入口

这些链接均可从本文件直接打开。游戏的目录、命名和注释规则是新项目约定，不要求修改 DragonLib 历史代码或第三方源码以统一格式。

| 主题 | 库内入口 |
| --- | --- |
| Engine 能力与可选 ECS、编辑器依赖 | [Engine 文档](Libs/Engine/README.md)、[Engine.csproj](Libs/Engine/Engine.csproj) |
| 平台判断与 Web 接入 | [平台属性](Libs/DragonLib.Platform.props)、[Web targets](Libs/DragonLib.Web.targets)、[Foster.Web](Libs/Foster.Web/README.md) |
| Web 发布后的本地预览 | [静态预览服务](Libs/Foster.Web/serve.py) |
| 游戏宿主 | [GameApp.cs](Libs/Engine/GameApp.cs) |
| Paper UI 接入 | [Paper 使用指南](Libs/Engine/Paper/README.md)、[输入适配](Libs/Engine/Paper/PaperInput.cs)、[画布渲染器](Libs/Engine/Paper/FosterCanvasRenderer.cs) |
| Origami 现成控件与主题 | [控件 API 入口](Libs/ThirdParty/Prowl/Origami/Origami.cs)、[控件实现](Libs/ThirdParty/Prowl/Origami/Widgets/)、[主题配置](Libs/ThirdParty/Prowl/Origami/OrigamiTheme.cs) |
| Paper 与字体相关依赖的维护 | [Prowl 维护说明](Libs/ThirdParty/Prowl/README.md) |
| 资源与用户存储 | [GameStorage.cs](Libs/Engine/Core/Storage/GameStorage.cs) |
| 实体基础存储与句柄 | [SlotMap.cs](Libs/Engine/Core/Structure/SlotMap.cs)、[SlotHandle.cs](Libs/Engine/Core/Structure/SlotHandle.cs) |
| 渲染与 Shader 工具 | [渲染文档](Libs/Engine/Rendering/README.md)、[编译脚本](Tools/ShaderCompiler/Build-Shaders.ps1)、[校验脚本](Tools/ShaderCompiler/Verify-Shaders.ps1) |
| 可选 ECS 与桌面编辑器库 | [Engine.ECS.csproj](Libs/Engine.ECS/Engine.ECS.csproj)、[Engine.Editor.csproj](Libs/Engine.Editor/Engine.Editor.csproj) |

库的综合示例与测试位于独立的 DragonLib.Tests 仓库，默认与 DragonLib 同级，详见 [仓库说明](README.md)。它们用于参考库 API；新项目的业务、资源和验证应保存在新游戏自己的仓库中。
