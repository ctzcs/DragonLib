# Foster.Web

Foster 的独立 **.NET 10 WebAssembly + WebGL2** 后端。参考 OFoster 的浏览器架构，使用 JavaScript 提供图形、DOM 输入和存储，由 `requestAnimationFrame` 驱动 C# 游戏。

所有新增实现、着色器、示例和打包脚本都在本目录。`../Foster` 的源码、项目和桌面 SDL3 后端无需修改；通用数学、Batcher、Mesh、纹理、字体、输入绑定等代码通过项目中的 `Compile Link` 复用。构建 Web 项目也不会构建或向原 Foster 项目写入 `bin/obj`。本目录依赖仓库里的 `../Foster/Framework`，不是该源码的完整复制。

`Framework/SpriteFont.cs` 是单独的平台副本，仅将 `AddCharacters` 改为顺序生成字形，避开浏览器不支持的 `Task.WaitAll`；同步升级 Foster 时需检查这个副本。`AppConfig.cs` 保留桌面配置类型的接口。

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

通过 `build.ps1 -Project <你的 csproj>` 发布。示例和后端暂时使用反射 JSON 生成绘制描述，必须保留 `PublishTrimmed=false`。

命名空间仍是 `Foster.Framework`，程序集仍是 `Foster.Framework.dll`。**一个游戏项目只能引用桌面 Foster 或 Foster.Web 中的一个。** 不能同时引用这两个项目，也不能间接引入已经编译过的桌面 Foster；其他库若引用桌面项目，需要单独建立 Web 构建配置并切换该引用。

### 生命周期

Web 的 `App.Run()` 在 `Startup()` 后立即返回；浏览器稍后调用 `Update/Render`。因此入口不要写 `using var app = ...; app.Run();`，也不要在 `Run()` 后立刻 `Dispose()`：

```csharp
var app = new MyGame();
app.Run(); // WebRuntime 在循环期间持有 App
```

`Exit()` 在当前帧结束时触发一次 `Shutdown()` 并停止循环；`Shutdown()` 负责释放游戏资源。窗口隐藏后可以显式调用 `Dispose()` 释放后端。后台恢复时帧时间最多 250ms，固定步长不会使用 `Thread.Sleep`。

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
| Foster.Audio / DragonLib Engine 的 Web 后端 | 本目录不包含；各自需要平台适配 |

本后端的 C# 绘制参数通过 JSON 传给 JS，GPU 数据通过 `MemoryView` 传输；这是可运行的 2D 起点，大量 draw call 的性能优化可以继续在本目录内推进。

项目设置和 JS 互操作方式参考 [Microsoft .NET WebAssembly 文档](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-10.0)。Foster 通用源码及内置着色器遵循本目录的 MIT `LICENSE`。
