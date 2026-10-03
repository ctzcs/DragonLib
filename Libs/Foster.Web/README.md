# Foster.Web

纹理格式、mipmap、采样器和线段拓扑类型复用固定 MyFoster 版本，来源见 `../Foster/UPSTREAM.md`。
Web 的 GPU/JS 实现独立维护；`ContentStorage.cs` 覆盖新版 Foster 的原生存储类，保留浏览器存储。
已有 `Storage` API 继续可用。新版 GPU 名称在浏览器报告 Unknown，调试标签忽略；wireframe 明确报告不支持。

Foster 的独立 **.NET 10 WebAssembly + WebGL2** 后端。参考 OFoster 的浏览器架构，使用 JavaScript 提供图形、DOM 输入和存储，由 `requestAnimationFrame` 驱动 C# 游戏。

所有新增实现、着色器、示例和打包脚本都在本目录。`../Foster` 的源码、项目和桌面 SDL3 后端无需修改；通用数学、Batcher、Mesh、纹理、字体、输入绑定等代码通过项目中的 `Compile Link` 复用。构建 Web 项目也不会构建或向原 Foster 项目写入 `bin/obj`。本目录依赖仓库里的 `../Foster/Framework`，不是该源码的完整复制。

`Framework/` 里与 Foster 源文件**同名**的文件(App、Window、ContentStorage、FileSystem、Cursor、ImageData、GraphicsDriver)替换对应的 Foster 源码，排除列表按文件名自动生成；只有 SDL 后端三个文件(GraphicsDeviceSDL、InputProviderSDL、SDL3)是显式排除的。新增替换文件时直接放进 `Framework/` 即可。共享源码需要的浏览器兼容改动先在独立 MyFoster 分支提交、验证并推送，再同步固定版本(例如 `SpriteFont.AddCharacters` 在 `OperatingSystem.IsBrowser()` 时顺序生成字形，避开 `Task.WaitAll`)。`Storage.cs` 保留旧版浏览器存储 API，`AppConfig.cs` 保留桌面配置类型的接口。

## 快速打包和运行

需要 .NET 10 SDK 及匹配的 WASM 工作负载，浏览器需要支持 WebGL2：

```powershell
dotnet workload install wasm-tools
cd D:\MySpace\Github\DragonLib\Libs\Foster.Web
.\build_web.bat
python .\serve.py
```

浏览器打开 `http://localhost:8138/`。默认产物为 `artifacts/web/wwwroot/`，把这个目录的**全部内容**部署到静态 HTTP(S) 服务即可。服务必须将 `.wasm` 作为 `application/wasm` 返回。不要通过 `file://` 打开页面。

示例先自动检查 PNG/QOI 往返、Batcher、离屏 Target、裁剪、纹理回读和 Blit；通过后页面显示 `PASS`。点击画布获得输入焦点，WASD/方向键移动，空格保存计数，刷新验证存档，F 请求全屏，Esc 退出。

Linux/macOS 可执行 `sh build_web.sh`。Windows 也可以直接使用 PowerShell：

```powershell
.\build.ps1 -Project .\Samples\WebDemo\WebDemo.csproj -Configuration Release
# 可选 AOT（首次较慢；基础验证使用非 AOT 发布）
.\build.ps1 -Aot
# 单独构建 / NuGet 包
dotnet build .\Foster.Web.slnx -c Release
dotnet pack .\Framework\Foster.Framework.Web.csproj -c Release -o .\artifacts\packages
```

发布失败时脚本会返回失败；原有产物不会被自动删除。不要在一次失败之后部署旧的产物。

## 接入自己的游戏

复制 `Samples/WebDemo` 的项目结构并换成自己的入口和 App 子类。保持以下配置：

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishTrimmed>false</PublishTrimmed>
    <WasmEnableHotReload>false</WasmEnableHotReload>
  </PropertyGroup>
  <ItemGroup>
    <!-- 按实际项目位置调整相对路径 -->
    <ProjectReference Include="../../Framework/Foster.Framework.Web.csproj" />
    <Content Include="../../Web/**"
             Link="wwwroot/%(RecursiveDir)%(Filename)%(Extension)"
             CopyToOutputDirectory="PreserveNewest"
             CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

通过 `build.ps1 -Project <你的 csproj>` 发布。后端发给 JS 的绘制描述走源生成的 `WebJson`(`Framework/WebJson.cs`)，不用反射，`Foster.Framework.Web` 标记为 `IsAotCompatible`，AOT 时也可被裁剪。示例自身若用反射 JSON 才需要 `PublishTrimmed=false`。`build.ps1` 每次发布前清掉旧的 `_framework`，`-Aot` 使用独立的中间目录(`obj/<配置>-aot/`)：解释执行与 AOT 共用中间目录时，增量原生链接会复用另一模式的产物，页面加载运行时即失败。

使用 DragonLib Engine 的游戏不必手写以上配置：入口项目导入 `../DragonLib.Web.targets`(见 Engine 的 README「Web 发布」)。

.NET 10 的 wasm AOT 强制要求 `PublishTrimmed=true`。AOT 发布时改用 `TrimMode=partial`（只裁 .NET 框架，游戏和本后端保留反射元数据）并设置 `JsonSerializerIsReflectionEnabledByDefault=true`；用 JSON 反序列化不可变集合时还要 `<TrimmerRootAssembly Include="System.Collections.Immutable" />`。`../DragonLib.Web.targets` 已包含这些设置。

AOT 下避免对结构体使用 LINQ（如 `colors.Select(c => ...)`，`c` 为 `Color`）：解释执行的泛型方法经 gsharedvt 包装回调 AOT lambda 时会出现 `memory access out of bounds`。后端内部已改为普通循环。排查原生崩溃时加 `-p:WasmEmitSymbolMap=true` 发布，用 `obj/.../dotnet.native.js.symbols` 对照报错里的 `wasm-function[N]`。

命名空间仍是 `Foster.Framework`，程序集仍是 `Foster.Framework.dll`。**一个游戏项目只能引用桌面 Foster 或 Foster.Web 中的一个。** 不能同时引用这两个项目，也不能间接引入已经编译过的桌面 Foster；其他库若引用桌面项目，需要单独建立 Web 构建配置并切换该引用。

DragonLib 的 `Engine` 与 `DragonLib.Box2D` 已按这种方式多目标：`net10.0` 引用桌面 Foster，`net10.0-browser` 引用本项目。游戏库同样写 `<TargetFrameworks>net10.0;net10.0-browser</TargetFrameworks>`，Web 入口项目写 `<TargetFramework>net10.0-browser</TargetFramework>`，引用链就会整条切到 Foster.Web；桌面入口只构建 `net10.0` 那一份。完整示例见 Proj_TD 的 `Scripts/Game.Web`。

### 生命周期

Web 的 `App.Run()` 在 `Startup()` 后立即返回；浏览器稍后调用 `Update/Render`。因此入口不要写 `using var app = ...; app.Run();`，也不要在 `Run()` 后立刻 `Dispose()`：

```csharp
var app = new MyGame();
app.Run(); // WebRuntime 在循环期间持有 App
```

`Exit()` 在当前帧结束时触发一次 `Shutdown()` 并停止循环；`Shutdown()` 负责释放游戏资源。窗口隐藏后可以显式调用 `Dispose()` 释放后端。后台恢复时帧时间最多 250ms，固定步长不会使用 `Thread.Sleep`。与桌面 `FixedWaitEnabled` 一致，固定步长下没有执行 `Update` 的浏览器帧（高刷新率屏幕或 rAF 抖动）不调用 `Render`，画布保留上一帧；否则只在 `Update` 中准备的 UI 等状态会隔帧缺失，表现为闪烁、画面发浅。

### 资源与存档

浏览器资源由 `Web/main.js` 在运行入口之前预加载。`Web/assets.json` 格式：

```json
[
  { "path": "Content/player.png", "url": "./Content/player.png" },
  { "path": "Content/level.json", "url": "./Content/level.json" }
]
```

资源实际文件也要放入项目 `wwwroot` 或用 `Content Include/Link` 打包到对应位置。URL 相对于网页位置解析，支持网站子路径部署。C# 通过 Foster 的 title storage 读取：

```csharp
FileSystem.OpenTitleStorage(storage => {
    using (storage) {
        using var stream = storage.OpenRead("Content/player.png");
        using var image = new Image(stream);
        var texture = new Texture(GraphicsDevice, image);
    }
});
```

不要使用 `File.ReadAllBytes("Content/...")` 加载 HTTP 资源；它不会自动访问预加载资源表。

托管代码的游戏数据一律经 `StorageContainer` 读取：桌面用 title storage 或自己的 `LocalStorage`，Web 用上面的 title storage。需要在创建 `App` 之前读取配置时（例如入口先加载地图、数值再决定怎样构造游戏），Web 可直接调用 `Storage.OpenTitleStorage(null)`：`main.js` 在 `Main` 之前就已预加载资源。

只有按路径打开文件的原生代码（如 Foster.Audio 的 `new Sound(path)`、流式播放）才给条目加 `"vfs": true`：`main.js` 会调用 `WebRuntime.AddFile` 把它写进 WebAssembly 内存文件系统（相对当前目录，即 `/`）。这类文件不进 title storage；写入只留在本次页面内存，刷新即丢失。

`OpenUserStorage` 对应按 App 名称隔离的 `localStorage`，二进制文件使用 Base64；Stream 的 `Flush/Dispose` 写入。配额不足或存储被浏览器禁用会抛出异常，不会伪装为保存成功。目录从文件路径推导，不保存空目录；不允许 `..` 越过根路径。`UserPath` 是逻辑路径，不能通过桌面 `System.IO` API 访问。

### 着色器

默认 Batcher/Textured/MSDF 已提供 GLSL ES 3.00 源码，作为程序集资源嵌入。自定义 Shader 的 `code` 需要 UTF-8 GLSL（`main` 入口），不能直接使用桌面的 `.spv/.dxil/.msl`。浏览器驱动枚举为 `GraphicsDriver.WebGL`，扩展名为 `.glsl`。

绑定约定：

| Foster 资源 | GLSL 名称 |
| --- | --- |
| 顶点 uniform slot N | `layout(std140) uniform VertexUniformN { ... };` |
| 片元 uniform slot N | `layout(std140) uniform FragmentUniformN { ... };` |
| 顶点 sampler slot N | `uniform sampler2D u_vertex_texN;` |
| 片元 sampler slot N | `uniform sampler2D u_fragment_texN;` |
| 顶点属性 | `layout(location = VertexFormat.Element.Index)` |
| 离屏坐标翻转 | `uniform float u_target_flip;`，将 `gl_Position.y` 乘以它 |

已有 SDL shadercross 着色器(HLSL → `.spv`)可以用 DragonLib 的 `Tools/ShaderCompiler/spv-to-glsl.ps1 -Spv X.vertex.spv -Output X.vertex.glsl` 转换(`Build-Shaders.ps1` 一次生成 dxil/spv/msl/glsl 并写哈希清单)(需要 Vulkan SDK 的 `spirv-cross`)。脚本按上表改名 uniform block、合并采样器和阶段间变量，并在顶点末尾加入 `u_target_flip`。SDL GPU 默认 depth clamp 而 WebGL 只有裁剪，脚本还会把 clip z 夹到 `[0, w]` 再映射到 GL 的 `[-w, w]`；否则 `CreateOrthographicOffCenter(…, 0.1f, 1000)` 投影下 z=0 的二维图元会被整批裁掉。手写 GLSL 时也要注意这一点。

矩阵 uniform 按 `Matrix4x4` 的字节布局传入；自定义结构要满足 GLSL `std140` 对齐。每个阶段最多 8 个 uniform slot。纹理逻辑第一行是顶部；默认顶点着色器在离屏绘制时翻转 Y，让上传、渲染、采样和回读采用同一个方向。

## 当前边界

| 功能 | 状态 |
| --- | --- |
| Batcher、Mesh、索引、实例化、混合、viewport/scissor | 已实现 |
| 颜色纹理、单采样 Target、多颜色附件、深度/模板状态 | 已实现，具体格式仍受浏览器限制 |
| 默认 SpriteFont/MSDF、托管 TTF、PNG/QOI | 已实现；PNG 支持非交错 8-bit，其他 PNG 应先转换成 RGBA8/QOI |
| 键鼠、滚轮、标准映射 Gamepad | 已实现；设备需要浏览器授权或用户操作后才会出现 |
| 全屏、Pointer Lock | 需要用户手势，初始请求会延迟到画布交互 |
| 同步剪贴板读取 | 返回本后端缓存；写入系统剪贴板取决于浏览器权限 |
| 存档、预加载资源、路径枚举 | 已实现 |
| Compute、StorageBuffer、Compute texture、多采样 Target | 明确抛出 `PlatformNotSupportedException` |
| 多窗口、窗口定位、鼠标瞬移、手柄震动、原生文件对话框 | 暂不支持 |
| IME/移动端软键盘 | 仅基础文字/组合事件，没有完整输入控件 |
| WebGL context 丢失后的自动重建 | 暂不支持；停止并显示错误，刷新重启 |
| DragonLib Engine | `net10.0-browser` 目标链接本项目；`JobScheduler`、`CliConsole` 等线程功能在浏览器上不可用；Dear ImGui 在 `Engine.Editor`，不进 Web 包 |
| 窗口位置、最大化 | 设回浏览器唯一的状态(`Position = (0,0)`、`Maximized = false`)是空操作，其他值抛出 `PlatformNotSupportedException` |
| Scribe MSDF 文字 | 原生 msdfgen 通过 `ThirdParty/Msdfgen/Msdfgen.Web.targets` 用 emcc 静态链接进 `dotnet.native.wasm` |
| Foster.Audio 的 Web 后端 | `Audio/Foster.Audio.Web.csproj` + `Audio/Foster.Audio.Web.targets` 静态编译 miniaudio；接入见下文 |

### 音频 WebAssembly 打包

`Audio` 目录复用原 `Foster.Audio` 的托管 API 和 miniaudio/解码器源码，用 WebAudio 输出。原来的桌面项目和原生库不需要修改。Web 游戏项目换成以下引用，并在**最终 WebAssembly 可执行项目**中导入原生编译目标（仅引用 C# 项目不能自动传递原生链接输入）：

```xml
<ItemGroup>
  <ProjectReference Include="你的相对路径/Libs/Foster.Web/Audio/Foster.Audio.Web.csproj" />
</ItemGroup>
<Import Project="你的相对路径/Libs/Foster.Web/Audio/Foster.Audio.Web.targets" />
```

使用 Engine 的 `net10.0-browser` 目标时已经间接引用 Web Audio，只需在最终 Web 项目加 `Import`。桌面 Engine 仍引用原 Foster.Audio。同一程序不要同时引用两个 Audio 项目，它们的程序集和命名空间都是 `Foster.Audio`。

游戏继续使用 `Audio.Startup()` / `Audio.Update()` / `Audio.Shutdown()`、`new Sound(...)`、`SoundInstance` 和 `SoundGroup`。原生代码由 .NET `wasm-tools` 工作负载内的 emcc 编译进 `dotnet.native.wasm`，无需复制桌面的 `.dll/.so`，也无需另外安装 Emscripten。第一次发布会重新链接运行时，耗时较长。

音频文件通过现有 `assets.json` 写入虚拟文件系统，才能沿用路径构造函数：

```json
[{ "path": "Content/music.ogg", "url": "./Content/music.ogg", "vfs": true }]
```

实际文件也必须作为 `Content` 打包到站点；随后 `new Sound("Content/music.ogg")` 可直接使用。`Sound(byte[])` 也可继续使用。源码保留 WAV、MP3、FLAC、Vorbis 和 QOA 解码器；实际浏览器验证范围见示例说明。

浏览器限制：

- 初始化后需要一次用户点击/触摸才能出声，miniaudio 会自动尝试解锁音频；建议游戏有“点击开始”界面。
- 当前使用单线程 WebAudio ScriptProcessor 后端，资源任务由浏览器定时器处理，不要求跨源隔离响应头。主线程长时间阻塞、后台标签页节流可能造成卡音；不适合在每帧做重解码。
- `SoundLoadingMethod.Stream` 从预加载的内存文件系统分段解码，不是从网络边下载边播放。
- 退出时先释放 `Sound` / `SoundGroup`，再 `Audio.Shutdown()`。适配层会关闭设备、停止资源任务并释放资源管理器。

音频验证示例（无需音频素材，启动时生成 WAV）：

```powershell
./Libs/Foster.Web/build.ps1 -Project ./Libs/Foster.Web/Samples/AudioDemo/AudioDemo.csproj -Output ./Libs/Foster.Web/artifacts/audio
python -m http.server 8140 --bind 127.0.0.1 --directory ./Libs/Foster.Web/artifacts/audio/wwwroot
```

打开 `http://127.0.0.1:8140/`，点击 Play 或 Play Stream。页面检查 WAV 解码、五种加载模式、循环/分组/空间参数，并显示音频回调次数和实际输出 PCM 峰值；Pause、Seek、Shutdown、Restart 用于验证生命周期。已在桌面 Chromium 中验证普通播放、流式循环持续输出非零 PCM、暂停归零、跳转及关闭后重启；Engine 两个目标均编译通过。非 WAV 格式、移动端浏览器和 AOT 发布尚未在此示例中验证。

实现依据：[.NET 原生 WebAssembly 依赖](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-native-dependencies?view=aspnetcore-10.0)、[miniaudio 单线程资源管理示例](https://miniaud.io/docs/examples/resource_manager.html)。复用源码及第三方许可证见 `../Foster.Audio/LICENSE` 和 `../Foster.Audio/Platform/src/third_party/`。

本后端的 C# 绘制参数通过 JSON 传给 JS，GPU 数据通过 `MemoryView` 传输；这是可运行的 2D 起点，大量 draw call 的性能优化可以继续在本目录内推进。

项目设置和 JS 互操作方式参考 [Microsoft .NET WebAssembly 文档](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-10.0)。Foster 通用源码及内置着色器遵循本目录的 MIT `LICENSE`。
