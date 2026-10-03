# Engine 与可选 ECS 扩展

不使用 ECS 的游戏只需引用 `Libs/Engine/Engine.csproj`。Engine 提供渲染、音频、资源、相机、动画计算、线程调度和通用工具，不引用 DragonECS。

使用 ECS 的游戏额外引用 `Libs/Engine.ECS/Engine.ECS.csproj`。该项目依赖 Engine、DragonECS 和 DragonECS-AutoInjections，提供组件、System、Pipeline 扩展、实体检查器，以及 ECS 预制体和关卡序列化。

```text
普通游戏 → Engine
ECS 游戏 → Engine.ECS → Engine
                     → Engine.Editor
                     → DragonECS / DragonECS-AutoInjections
编辑器/调试界面 → Engine.Editor → Engine(+ ImGui.NET)
```

Dear ImGui(`Engine.DearImGui` 的 Renderer、PropertyDrawer，以及 `Engine.Assets.AssetRefDrawer`)在 `Libs/Engine.Editor`。它依赖原生 cimgui，只有桌面版；游戏运行时不引用，Web 包也不会带上它。命名空间不变，使用方加 `Engine.Editor.csproj` 引用即可。

## 通用功能

- `Engine.World.SceneRouter<TScreen>`：场景切换及过渡进度，由游戏调用 `Update(dt)`。
- `Engine.Messaging.CommandQueue<TCommand>`：单消费者命令队列，由消费者调用 `Drain`。
- `Engine.Messaging.BroadcastChannel<TMessage>`：下一帧可见的广播消息；普通游戏在每帧开始、生产者和消费者运行之前调用一次 `AdvanceFrame()`。
- `Engine.Animation.SkeletonAnimator`：独立的骨骼动画计算；ECS 扩展中的 `AnimationSystem` 负责读取组件并调用它。

ECS 游戏通过 `using Engine.ECS;` 使用 `AddCommandQueue` 和 `AddBroadcastChannel` 接入 Pipeline；`AddBroadcastChannel` 会自动在每次 Update 开始时推进消息，不需要游戏再手动调用 `AdvanceFrame()`。

## 资源与存档(GameStorage)

游戏经 `Engine.GameStorage` 读写，不直接用 `System.IO`，同一份代码可在桌面和 Web 上运行：

- `GameStorage.Resources`：资源。桌面默认当前目录(开发时把 `RunWorkingDirectory` 设为项目根，编辑器改的文件立即可见)，Web 默认是 `main.js` 预加载的 title storage。不需要 App，可在创建游戏之前读取配置。发布版要读 exe 目录时 `GameStorage.UseResources(StorageUtils.GetReleaseGameRoot)`；`ResourcesChanged` 事件供不依赖 Foster 的数据层同步自己的读取入口。
- `GameStorage.WithUserStorage(app, storage => ...)`：设置、存档、截图。桌面在 `%APPDATA%/<应用名>`，Web 在 localStorage；用完即释放。`UserFilePath` 给出可显示的路径。
- `DassetModelLoader.Load(device, storage, path)` 接受任意 `StorageContainer`。

## Web 发布

`Engine` 同时编译 `net10.0`(桌面 Foster)与 `net10.0-browser`(Foster.Web)。游戏库同样写 `<TargetFrameworks>net10.0;net10.0-browser</TargetFrameworks>`，在 `Directory.Build.props` 导入 `Libs/DragonLib.Platform.props`，即可用 `'$(IsBrowser)' == 'true'` 和 `#if BROWSER` 区分平台(浏览器构建还会启用分析器 DLWEB001：对自定义结构体用 LINQ 在 wasm AOT 下会崩溃，改用循环)。

Web 入口项目(`Sdk="Microsoft.NET.Sdk.WebAssembly"`，`net10.0-browser`)引用游戏库、声明 `WebResource`，再导入 `Libs/DragonLib.Web.targets`：

```xml
<WebResource Include="..\Resources\**\*.*" Prefix="Resources" />
<WebResource Include="..\Resources\Music\*.wav" Prefix="Resources/Music" Qoa="true" />  <!-- 长音频转 QOA，约 1/5 大小 -->
<Import Project="..\..\DragonLib\Libs\DragonLib.Web.targets" />
```

它提供浏览器运行设置、Foster.Web 页面(项目目录有 `index.html` 时用项目的)、msdfgen 与 Foster.Audio.Web 原生库的静态链接、`assets.json` 生成，以及 AOT 所需的裁剪设置。完整示例与发布脚本见 Proj_TD 的 `Scripts/Game.Web` 和 `publish_web.bat`(AOT 用独立中间目录)。

着色器用 `Tools/ShaderCompiler/Build-Shaders.ps1` 从 HLSL 一次生成 dxil/spv/msl/glsl 与哈希清单，`Verify-Shaders.ps1` 校验；`EmbeddedShaderMaterial` 按当前驱动加载对应扩展名。

## 从旧版迁移

1. 使用 ECS 的项目添加 `Engine.ECS.csproj` 引用。原有 ECS 组件、System 和序列化类型继续使用 `Engine.ECS` 命名空间，预制体和关卡 JSON 格式保持不变。
2. 使用 `SceneRouter` 的文件改为导入 `Engine.World`。
3. 使用 `CommandQueue` 或 `BroadcastChannel` 的文件导入 `Engine.Messaging`；调用 Pipeline 扩展时同时导入 `Engine.ECS`。
4. 使用 Dear ImGui(`Engine.DearImGui`、`AssetRefDrawer`)的项目添加 `Engine.Editor.csproj` 引用(Engine.ECS 已引用)。
5. `Box2DWorld` 的多线程构造函数改为接收 `IBox2DTaskScheduler`；用 Engine 的 `JobScheduler` 时包一层适配器(示例见 `Tests/Game0/Content/JobSchedulerBox2DTasks.cs`)。
6. `DassetModelLoader.Load` 的存储参数从 `LocalStorage` 放宽为 `StorageContainer`，原调用不变。

新增通用能力时，先让它能在 Engine 中独立调用，再在 Engine.ECS 中提供读取组件、注入依赖和驱动更新的接入代码。

## 验证

```powershell
dotnet test Tests/Engine.Tests/Engine.Tests.csproj
dotnet test Tests/Game0/Tests/Entities.Tests.csproj
dotnet build Tests/Game0/Game0.sln
```

`Engine.Tests` 只引用 Engine，验证通用消息行为，并检查应用的依赖清单中没有 Engine.ECS 或 DragonECS。Game0 测试覆盖 ECS 接入及现有游戏功能。
