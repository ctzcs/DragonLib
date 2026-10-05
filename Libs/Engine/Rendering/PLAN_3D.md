# DragonLib 3D 功能补全计划

> 执行对象：自动化编码 agent。按阶段顺序执行，**每个阶段独立可构建、可测试、单独提交**。
> 遇到本文与代码现状不符时，以代码为准，记录差异后继续；遇到需要设计取舍且本文没写明的，停下来列出选项询问用户。

---

## 0. 开工前必读

### 0.1 仓库布局

```text
Github/
├── DragonLib/                 # 库（本仓库）
│   ├── Libs/Engine/           # 渲染、资产、动画、相机（不依赖 ECS）
│   ├── Libs/Engine.ECS/       # ECS 组件与系统（Transform3D、AnimationSystem…）
│   ├── Libs/Foster/           # 第三方桌面后端（SDL_GPU），仅同步已推送 MyFoster 的固定提交；优先在 Engine 适配
│   ├── Libs/Foster.Web/       # Web 后端（WebGL2），vendored，可改
│   ├── Libs/ThirdParty/Gltf/  # GltfModelCooker（glTF → .dasset，离线）
│   └── Tools/ShaderCompiler/  # Build-Shaders.ps1 / Verify-Shaders.ps1
└── DragonLib.Tests/           # 相邻仓库：Game0 demo、测试、3D shader 现在的位置
    ├── Game0/Shaders/         # Standard3D.hlsl / Standard3DSkinned.hlsl / DepthOnly.hlsl …
    ├── Game0/Content/Demos/   # GltfSceneDemo / SkinningDemo / GltfModelDemo / LightSandboxDemo / ThreeDDemo
    └── Game0/Tests/           # WindingTests / SkinningTests / Renderer3DSortTests …
```

### 0.2 必须遵守的约定（违反会出现内外颠倒、光照反向这类问题）

先通读 `Libs/Engine/Rendering/README.md`，重点：
- 优先遵循 Foster 既有设置，桌面后端保留默认 CW 正面。资产与 CPU 数据采用 CCW，cook 时只补偿负行列式；Engine 的 MeshUpload3D 在桌面上传时转换为 CW，Web 保留原序。
- 矩阵用**行向量**约定：`v' = v × M`；shader 中写 `mul(M, v)`，**不要 transpose**。
- 蒙皮 palette：`palette[i] = IBM[i] × global[i]`。
- 阴影 pass 用 `CullMode.Front`，不做视锥剔除。
- 改动任何约定，必须同步更新 README 和对应的锁定测试。

Shader 注意事项（来自现有 shader 的注释）：
- **每张贴图配独立的 `SamplerState`**：多张贴图共用一个 sampler 时，shadercross 会把它们识别成 storage texture，而 Foster 不绑定 storage texture。
- 寄存器约定：vertex uniform 用 `space1`，fragment 贴图和 sampler 用 `space2`，fragment uniform 用 `space3`。
- 每个 shader 都要能编译出 dxil、spv、msl、glsl 四种产物（glsl 给 WebGL2 用）。改完运行 `Verify-Shaders.ps1`。
- uniform 数组大小要兼容 WebGL2：单个 uniform block 不超过 16KB。

### 0.3 工作区状态

DragonLib 工作区里有未提交的改动：`Tests/` 目录整体删除（已迁到 DragonLib.Tests），以及若干 README 修改。**不要回滚，也不要混进你的提交**。只 `git add` 你自己改动的文件。

### 0.4 每个阶段的通用验收

```powershell
# 在 DragonLib 根目录执行
dotnet build ../DragonLib.Tests/DragonLib.Tests.slnx
dotnet build Libs/Engine/Engine.csproj -f net10.0-browser
dotnet test ../DragonLib.Tests/Engine.Tests/Engine.Tests.csproj
dotnet test ../DragonLib.Tests/Game0/Tests/Entities.Tests.csproj
```

- 四条命令全部通过；新增功能要有单元测试（纯数学或纯数据逻辑必须测，GPU 部分至少保证构建通过）。
- 改过 shader 就重新生成产物，并通过 `Verify-Shaders.ps1` 校验哈希清单。
- 涉及画面变化时，在提交说明里写清楚应该在哪个 demo 看什么效果，交给用户目视确认（agent 无法确认画面）。
- 代码风格：沿用现有写法，注释用中文、说明「为什么」，新代码的注释密度与周边一致。
- 浏览器构建开启了分析器 DLWEB001：不要对自定义结构体使用 LINQ，改用循环。

---

## 阶段 1：把 3D 标准管线收进 Engine（最高优先级）

**目标**：游戏只引用 `Engine.csproj` 就能渲染带 PBR、阴影、点光和蒙皮的 glTF 模型，不再从 Game0 复制 shader 和 uniform 结构体。

### 1.1 Shader 迁移与去重
1. 新建 `Libs/Engine/Rendering/Shaders/`，从 `DragonLib.Tests/Game0/Shaders/` **移动**（不是复制）以下文件：
   - `Standard3D.hlsl`
   - `Standard3DSkinned.hlsl`
   - `DepthOnly.hlsl`
2. 把两个 Standard3D 文件的公共部分（cbuffer、贴图声明、`ComputeShadow`、GGX 三个函数、`ShadeCookTorrance`、`fragment_main`）抽到 `Standard3DCommon.hlsli`，两个入口文件各自 `#include`，只保留 VsInput 和 `vertex_main` 的差异。
   - 先写最小的 include 测试，确认 shadercross 支持 `#include`（相对路径）。**如果不支持**：改为在 `build.ps1` 里做预处理拼接（把 `.hlsli` 内容内联成临时文件再编译），并在注释里写明原因。
3. 新增 `DepthOnlySkinned.hlsl`：读取同一个 palette cbuffer（`b2, space1`，`MAX_JOINTS` 与 `SkeletonAnimator.MaxJoints` 保持一致），输出蒙皮后的深度。
4. 仿照 `Libs/Engine/Paper/Shaders/build.ps1` 新建 `Libs/Engine/Rendering/Shaders/build.ps1`，调用 `Tools/ShaderCompiler/Build-Shaders.ps1` 生成 `Compiled/` 和 `.sha256` 清单。
5. 在 `Engine.csproj` 中嵌入资源，LogicalName 用 `Engine/Shaders/%(Filename)%(Extension)`，避免与 Paper 的 `Shaders/` 冲突。

### 1.2 C# 侧封装（全部放在 `Libs/Engine/Rendering/`）
1. `Standard3DUniforms.cs`：把 GltfSceneDemo 和 SkinningDemo 里重复定义的 `LightUniforms`、`MaterialUniforms`、`ShadowMatrixUniforms`、`ShadowSettingsUniforms` 收成 public 结构体（`[StructLayout(Sequential, Pack = 4)]`），并在注释中标明对应的 cbuffer 槽位。
2. `Standard3DShaders.cs`：用 `EmbeddedShaderMaterial.Load` 从 Engine 程序集加载 Standard3D、Standard3DSkinned、DepthOnly、DepthOnlySkinned 四组 shader，并把 `ShaderStageSpec`（sampler 和 uniform 数量）集中写在这里。实现 `IDisposable`，持有一张 1×1 的白色贴图作为默认贴图。
3. `SceneLighting3D.cs`：方向光（方向、颜色）、环境光、点光列表（复用 `PointLight3D.Pack`）、阴影设置（是否开启、bias、darkness，以及 `ShadowMap` 引用）。提供 `Apply(Material material, Camera3D camera)`，写入 fragment b0、b2、b3、vertex b1 和 shadow sampler 槽位。
4. `StandardMaterial3D.cs`（或在 `DassetModelAsset` 上加扩展方法）：由 `DassetMaterial` 加上模型贴图表生成 `(Material, RenderState3D)`。蒙皮和静态 primitive 分别 clone 对应的 shader。生成结果按模型缓存。逻辑参照 `GltfSceneDemo.GetMaterials`。
5. `Renderer3D` 改动：
   - 新增 `SetLighting(SceneLighting3D lighting)`。设置后，在 End() 中提交每个 draw 前自动 `Apply` 到该 draw 的材质上，调用方不再手动逐材质写光照。
   - `SetShadowPass` 增加可选参数 `Material? skinnedDepthMaterial`。传入后，蒙皮 draw 进入阴影 pass：用 skinned depth 材质，并把 palette 写到 vertex slot 2。同步修改 `SubmitShadow` 的注释和 Rendering README 中的「阴影 pass」一节。
   - 新增便捷方法 `DrawModel(DassetModelAsset model, IReadOnlyList<(Material, RenderState3D)> materials, in Matrix4x4 world, ReadOnlySpan<Matrix4x4> palette = default, int meshIndex = -1)`，统一处理静态和蒙皮 primitive，以及 bounds 剔除。

### 1.3 ECS 接入（`Libs/Engine.ECS/`）
1. `MeshRenderSystem`：提供静态方法 `Submit(EcsWorld world, AssetDatabase assets, Renderer3D renderer, Standard3DShaders shaders, MaterialCache cache)`。遍历 `MeshRendererComp` 和 `LocalToWorldComp`，有 `SkinPaletteComp` 时走蒙皮路径。另写一个薄的 `IRenderSystem` 外壳。**Begin/End 和渲染目标的管理仍由游戏负责**，系统只往一个已经 Begin 的 renderer 里排队 draw。
2. 光源组件：`DirectionalLight3DComp`（配置层）、`PointLight3DComp`（配置层，位置取自 `LocalToWorldComp`），外加一个收集函数，把它们写入 `SceneLighting3D`。新组件按需登记到 `EcsComponentLayers.cs`，并确认能被 `LevelSerializer` 序列化。

### 1.4 迁移 demo（在 DragonLib.Tests 仓库）
- 把 `GltfSceneDemo`、`SkinningDemo`、`GltfModelDemo` 改为使用上面的库 API，删除它们内部重复的 uniform 结构体和材质组装代码。
- 从 `Game0/Shaders/` 删除已移走的三个 shader 及其 `Compiled/` 产物。`LightSandbox`、`Basic3D*` 等 demo 专用 shader 保留。
- GltfSceneDemo 现在会跳过蒙皮 primitive（「本 demo 的管线不演示蒙皮」），改用 `MeshRenderSystem` 后应能正常画出蒙皮模型。

### 1.5 测试
- 新增 `MeshRenderSystem` 的筛选和排队逻辑测试（不碰 GPU 的部分抽成纯函数测）。
- 新增 `SceneLighting3D` 的打包布局测试（字节偏移与 cbuffer 对齐）。
- 新增 uniform 结构体的 `Marshal.SizeOf` 断言（必须是 16 的倍数，并与 hlsl 一致）。
- 现有的 `WindingTests`、`SkinningTests`、`SkinningLightingTests` 必须全部保持通过。

**验收**：三个 demo 画面与迁移前一致；SkinningDemo 里的蒙皮角色开始投射阴影；Game0 中不再有 Standard3D 相关的 hlsl 文件。

### 执行记录（2026-10-03）

- 阶段 1 API、shader 迁移、ECS 接入、三个 demo 迁移及八项新测试已完成。
- 四条通用验收命令通过（Engine 3 项、实体/渲染 90 项）；四组 shader 的 dxil/spv/msl/glsl 和 include 哈希通过。
- DragonLib 提交 `b2db407`，DragonLib.Tests 对应提交 `60cd0a3`；原有 README 路径迁移及 Tests 删除未混入提交。
- 画面仍需用户目视核对 GltfScene、GltfModel 和 Skinning 的动画阴影；阶段 2 GPU smoke 另发现现有 front-face 与 CCW 文档冲突，见后续记录。

---

## 阶段 2：色彩管线（线性空间 + HDR + tonemapping + mipmap）

**背景**：Foster 的 `TextureFormat` 只有 `R8G8B8A8 / R8 / R8G8 / Depth*`，没有 sRGB、没有浮点格式；`Texture` 也不生成 mipmap。目前 3D 光照实际上在 gamma 空间计算，片元最后直接 `saturate`。

### 2.1 Foster 扩展（桌面和 Web 两个后端都要做）
1. **先做可行性调查，写一份简短报告再动手**：
   - SDL_GPU：`R8G8B8A8_UNORM_SRGB`、`R16G16B16A16_FLOAT` 能否作为采样贴图和颜色附件使用；`SDL_GenerateMipmapsForGPUTexture` 的用法。
   - WebGL2：`SRGB8_ALPHA8`、`RGBA16F` 作为颜色附件需要 `EXT_color_buffer_float`；`generateMipmap` 的限制。
   - 桌面后端看 `Libs/Foster/Framework/Internal/GraphicsDeviceSDL.cs`，Web 后端看 `Libs/Foster.Web/Framework/GraphicsDeviceWeb.cs`。
2. 在 `TextureFormat` 新增 `R8G8B8A8Srgb` 和 `R16G16B16A16Float`，在 `TextureFlags` 新增 `GenerateMipmaps`。`TextureSampler` 需要支持 mip 过滤时一并加上。**改动尽量小**，并在新增代码旁注明「DragonLib 扩展」，方便将来与上游合并。
3. `IsTextureFormatSupported` 要如实返回结果；不支持时调用方走回退路径。

### 2.2 Engine 侧
1. `DassetModelLoader`：albedo 和 emissive 贴图用 sRGB 格式，法线、metallicRoughness、AO 贴图用线性格式；全部生成 mipmap。为此材质需要知道每张贴图的用途，可以在 loader 里按材质引用关系推导。
2. `RenderTarget3D`：可选 HDR 颜色格式（`R16G16B16A16Float`），不支持时退回 `Color`。
3. 新增 `Tonemap.hlsl` 和 `Tonemapper3D`：把 HDR 目标合成到屏幕，支持 ACES（近似）和 Reinhard，带曝光参数，输出时做 sRGB 编码。如果 swapchain 本身是 sRGB 格式，就不要重复编码。`RenderTarget3D.Composite` 保留原来的 LDR 路径。
4. `Standard3DCommon.hlsli`：去掉最终的 `saturate`；HDR 关闭时保持当前观感（可以用宏或 uniform 开关）。

**验收**：Windows 桌面和浏览器构建都能运行，GltfSceneDemo 增加 HDR/LDR 切换开关；远处地面网格的贴图闪烁明显减轻（mipmap 生效）。

### 执行记录（2026-10-03）

- 先完成 `COLOR_PIPELINE_FEASIBILITY.md` 后实现两后端 sRGB/HDR 格式、mipmap、mip sampler、用途区分、HDR 目标和 ACES/Reinhard 合成；GltfScene 已有 HDR/曝光开关。
- 四条通用验收命令通过（Engine 3 项、实体/渲染 101 项），五组 shader 哈希及 GLSL ES 编译校验通过。
- D3D12 实际读回 HDR 亮度 4，ACES 输出 252/255；同时发现 CCW 三角形被现有 Cull.Back 剔除，旧 shader 路径同样复现。
- 用户明确正面应为 CCW 后，已修正 SDL front-face 并更新 Rendering README；新增 Rendering3D.Smoke 的 CCW/CW × Back/Front 对照，在 D3D12、Vulkan 均通过。阶段 2 就绪，继续阶段 3。
- 用户已明确允许下载常见标准样例，覆盖阶段 3 原先「不要自行下载」的限制；准备 Khronos DamagedHelmet 用于材质验证。

---

## 阶段 3：材质补全（metallicRoughness / AO / emissive）

1. `DassetMaterial` 新增字段：`MetallicRoughnessTextureIndex`、`OcclusionTextureIndex`、`OcclusionStrength`、`EmissiveTextureIndex`、`EmissiveFactor (Vector3)`、`NormalScale`。
2. `.dasset` 格式升到 **v3**：在 `DassetFormat.Version` 写明 v3 新增的字段，`DassetReader` 继续兼容 v1 和 v2（缺失字段取默认值），`DassetWriter` 写 v3。补充往返测试和旧版本读取测试（`DassetTests.cs`）。
3. `GltfModelCooker` 读取 glTF 的 `metallicRoughnessTexture`（B 通道为 metallic，G 通道为 roughness）、`occlusionTexture`、`emissiveTexture`、`emissiveFactor`、`normalTexture.scale`。
4. Shader：贴图槽位从 3 个扩到 6 个（albedo、normal、shadow、metallicRoughness、occlusion、emissive），**每张贴图配独立 sampler**；材质 cbuffer 增加相应的 flags。同步修改 `Standard3DShaders` 里的 `ShaderStageSpec` 和材质组装代码。
5. 用 Khronos 的 `DamagedHelmet` 或类似的标准样例做 cook 测试（检查样例是否已在仓库资源中；不在的话请用户提供，**不要自行下载**）。

**验收**：带 MR/AO/emissive 贴图的模型渲染正确；旧的 `.dasset` 文件仍能加载。

---

### 执行记录（2026-10-03）

- 阶段 3 完成 v3 材质布局、兼容 v1/v2、六贴图 sampler、颜色用途区分；DamagedHelmet 已烘焙并接入 GltfModel 开关，来源和许可随资源保存。
- 四条验收命令通过（Engine 3 项、实体/渲染 106 项），shader 哈希和 D3D12 CCW/HDR smoke 通过。
- 目视确认：GltfModel 勾选 DamagedHelmet，检查金属表面、法线凹凸和自发光。

## 阶段 4：阴影增强

1. **级联阴影（CSM）**：3 或 4 级，按视锥分段（对数和线性混合，可调 λ），用 shadow atlas（一张大深度图分成 2×2 区块）实现，避开纹理数组以兼容 WebGL2。
   - 对每一级做 texel snapping，消除相机移动时阴影边缘闪烁。
   - `ShadowMap` 保留现在的「单张图 + 场景包围球」模式作为简单模式；新增 `CascadedShadowMap`。
   - shader 在片元阶段按视空间深度选择级联，相邻级联之间可选混合。
2. **实例化 draw 的阴影**：`DrawInstances` 增加可选参数 `Material? instancedDepthMaterial`，由调用方提供与实例布局匹配的深度 shader；不提供时保持现在的行为（不投阴影）。
3. 新增 debug 开关：用不同颜色可视化各级联范围。
4. 测试：级联分段计算和 texel snapping 都是纯数学，必须写单元测试。

---

### 执行记录（2026-10-03）

- 阶段 4 完成四级 CSM atlas、混合分段、世界光空间 texel snapping、重叠混合、debug colors、实例深度材质参数及 GltfScene 开关。
- 修复原阴影采样 y 方向；D3D12/Vulkan 的非对称遮挡物实际读回在简单/CSM 两模式均通过。
- 四条通用验收通过（Engine 3 项、实体/渲染 111 项），shader 哈希和 GLSL 校验通过；接口说明见 FEATURES_3D.md。

## 阶段 5：拾取与 3D 调试绘制

1. `Engine.World`：
   - `Ray3D` 结构体；`Camera3D.ScreenPointToRay(Vector2 pixel)`，用 ViewProjection 的逆矩阵反投影。注意 NDC 的 y 轴朝上，像素坐标的 y 轴朝下。
   - 相交检测工具：射线与 AABB（slab 法）、射线与三角形（Möller–Trumbore）、射线与球、射线与平面。
   - `DassetModelAsset` 增加 CPU 侧的几何保留选项（默认关闭，开启后才能做逐三角形拾取）。
2. `Engine.Rendering.DebugDraw3D`：立即模式画线（line list），提供 `Line`、`Aabb`、`Sphere`（3 个圆）、`Frustum`、`Axis`、`Grid`、`Skeleton(DassetSkeleton, palette 或 globals)`。新增 shader `DebugLine3D.hlsl`，可选择是否做深度测试。
3. 测试：屏幕中心射线方向与相机 Forward 一致；四个屏幕角的射线与视锥边缘一致；各种相交函数的边界情况。
4. 在 GltfSceneDemo 中加入：左键点选实体并高亮其 AABB、显示骨骼、显示点光范围的开关。

---

### 执行记录（2026-10-03）

- 阶段 5 完成相机反投影、四种相交、蒙皮/静态三角形拾取、可选 CPU 几何、两后端 line list 和 DebugDraw3D；GltfScene 增加左键 AABB 选中、骨骼和光范围显示。
- 四条验收通过（Engine 3 项、实体/渲染 116 项），shader 哈希、GLSL、JS 语法校验通过；D3D12/Vulkan 读回 18 个调试线像素，并验证 CPU 几何默认关闭/显式开启。

## 阶段 6：动画系统增强

1. **插值补全**：`GltfModelCooker` 和 `SkeletonAnimator.SampleChannel` 支持 STEP 与 CUBICSPLINE（存 in-tangent、value、out-tangent 三元组；四元数插值后要归一化）。新增 channel 的插值类型字段，`.dasset` 版本号随之升级（可以和阶段 3 合并为同一次升级，视执行顺序而定）。
2. **交叉淡入淡出**：`SkeletonAnimator.BlendPoses(a, b, t, result)`，平移和缩放用 lerp，旋转用 slerp（先处理四元数符号，走最短路径）。`AnimatorComp` 扩展 `NextClipIndex`、`BlendDuration`、`BlendTime`，由 `AnimationSystem` 驱动过渡。
3. **提高关节上限**：`MaxJoints` 从 64 提到 128（128 × 64B = 8KB，在 WebGL2 的 16KB 限制内），shader 与 C# 两侧同步修改；cooker 的超限警告也同步更新。
4. **蒙皮包围盒**：可选择每帧由 palette 变换 bind AABB 的近似值，或在 cook 时按所有剪辑采样出最大包围盒（推荐后者，开销为零），解决动画姿态超出 bind pose 包围盒时被误剔除的问题。
5. 测试：混合权重为 0 或 1 时退化为单剪辑；CUBICSPLINE 在关键帧处精确等于关键值；四元数符号翻转不会导致绕远路。

---

### 执行记录（2026-10-03）

- 阶段 6 完成 STEP/CUBICSPLINE、v4 写入和 v1–v3 兼容、TRS 最短路径混合、Animator 过渡及 Skinning 的 wave/rest 按钮；关节上限 128，当前 palette 联合 bind AABB 保守覆盖姿态与混合。
- 发现计划中的「单个 8KB UBO」在 SDL 3.4 Vulkan 不可行：源码 MAX_UBO_SECTION_SIZE 为 4096，descriptor range 固定 4KB，实际第 127 关节读回失败。保持 128 关节 API，改用 vertex b2/b3 两块各 4KB，顶点槽总数 4。记录见 COLOR_PIPELINE_FEASIBILITY.md。
- 两驱动实际验证 CCW/CW × Back/Front、HDR/tonemap、简单/CSM 阴影 y 方向、debug line 和第 127 号关节颜色/蒙皮深度。
- 四条通用验收通过（Engine 3 项、实体/渲染 124 项）；六组 shader 的四种产物及哈希、GLSL 和 JS 校验通过。浏览器/Metal 实际画面未验证，需用户目视检查 demo。

## 暂不做（Backlog，等用户确认后再排期）

- IBL 与天空盒（依赖阶段 2 的 HDR 和浮点贴图，还需要 cubemap 支持，Foster 当前没有）
- 后处理：FXAA、bloom、SSAO、雾
- 聚光灯、聚簇光照（超过 16 盏点光时）
- 根运动、动画事件、IK、动画状态机、morph target、节点（非关节）动画
- cook 后保留节点层级（目前静态 primitive 的节点变换已烘焙进顶点）
- 贴图压缩（KTX2/Basis）
- LOD、遮挡剔除
- 3D 物理（BepuPhysics / Jolt 绑定）、3D 空间音频、编辑器 gizmo

---

## 提交规范

- 每个阶段至少一个提交，大阶段可以拆成 1.1 / 1.2 / … 分别提交；DragonLib 和 DragonLib.Tests 两个仓库**分别提交**，提交信息里互相注明对应关系。
- 提交说明写清：做了什么、哪些约定或格式有变（例如 dasset 版本号）、用户需要在哪个 demo 目视确认什么。
- 某个阶段做不完时，在本文件对应阶段下追加「执行记录」：已完成什么、卡在哪里、建议的下一步。

## 完成与提交对应表（2026-10-03）

| 阶段 | DragonLib | DragonLib.Tests |
| --- | --- | --- |
| 1 标准管线 | b2db407 | 60cd0a3 |
| 2 色彩与 CCW | a20d67a | acaf75e |
| 3 PBR 材质 | b69d3b9 | f352f57 |
| 4 阴影 | e77ca46 | 2baffa6 |
| 5 拾取与调试 | 968f2b0 | 33e657e |
| 6 动画 | 754fb9b | 4ec4906 |

最终四条通用验收全部通过，127 项 CPU 测试通过；D3D12/Vulkan 实际 GPU 回归通过。原有 Tests 删除、README 路径迁移等用户改动保留；本计划文件沿用开工时未跟踪状态，未混入阶段提交。实现文档为 FEATURES_3D.md，跨后端约束记录为 COLOR_PIPELINE_FEASIBILITY.md。浏览器/Metal 仅构建/产物验证，实际画面待目视验收。

## 后续调整：遵循 Foster 默认正面（2026-10-03）

用户要求优先遵循 Foster 框架的既有设置。已撤销阶段 2 的 front-face 改动，桌面恢复上游 CW；
Engine 新增 MeshUpload3D，在 GPU 上传边界把 CCW 源索引适配为 CW。Web 保留既有设置与 CCW 上传。
Mesh3D、自建地面网格、Dasset 静态/蒙皮加载统一使用该入口，模型文件、CPU 拾取数据、法线和切线无需迁移。
GPU smoke 分别验证原生和适配后的八组绕序/剔除组合、静态/蒙皮加载、CPU 源索引不变，原有阴影/CSM/HDR/关节 127 检查保留。
四条通用验收通过，127 项 CPU 测试通过；D3D12/Vulkan 实际 GPU 回归通过，Web 构建通过。浏览器/Metal 实际画面仍待验证。

## 后续调整：撤回第三方 Foster 扩展（2026-10-04，覆盖上述桌面能力记录）

用户明确第三方 Foster 原库不得承载本计划的修改，自有 Web 后端可改。
`Libs/Foster` 中六个改动文件已恢复到计划前 `0466793`，目录比较差异为零。
Web 的五个共享类型改为 Foster.Web/Framework 独立覆盖文件，原有 Web GPU/JS 扩展保留。
Engine 增加 TextureSupport3D 区分桌面/Web 能力：桌面 HDR、硬件 sRGB、mipmap 扩展撤回，使用 LDR；
调试线在 Engine 展开为三角形带，面绕序在 Engine 上传边界适配。Web 保留对应扩展。
阶段 2 的桌面 HDR/mipmap 验收不再代表当前能力；桌面 smoke 改为验证 LDR 回退。
本轮不新增提交，既有阶段提交保留历史，用户可查看工作区回退差异。

回退后四条通用验收通过（Engine 3 项、实体/渲染 120 项；mip 计算测试只在 Web 编译目标保留），
D3D12/Vulkan GPU smoke 通过，桌面调试线读回仍为 18 个红色像素。Web/Metal 实际画面待验证。

## 后续约定：通过独立 Foster/MyFoster 管理必要定制（2026-10-04）

用户允许在确需修改框架时，先在本地独立 Foster 仓库的 MyFoster 分支修改、验证并推送，再同步到 DragonLib。
已确认本地仓库为 `D:/MySpace/Github/Foster`，当前分支 MyFoster，origin 为 ctzcs/Foster，upstream 为 FosterFramework/Foster。
优先在 Engine 适配；必要框架改动在 MyFoster 保留独立提交，同步时记录上游基线和定制 SHA，以便比较上游更新与项目定制。
当前保留已完成的 Foster 回退和 Engine 适配，本次只记录工作流，未向远程推送。

## 后续执行：同步已推送的 MyFoster 定制（2026-10-04，当前状态）

用户授权继续实施后，在独立 `D:/MySpace/Github/Foster` 的 MyFoster 分支完成并推送：

| 定制 | 提交 |
| --- | --- |
| 保留原有 NoWindowFocus 和逐帧输入时序 | `0d2d5bb` |
| HDR/sRGB 格式、可选 mipmap、线段拓扑 | `c66d962` |
| 保留浏览器字体同步栅格化，沿用上游 alpha/pixel 参数 | `08c3d7f` |

官方上游基线为 Foster 0.4.2 的 `730cc6aec20068bf5cc57235b7aa70957f5d9035`。
DragonLib 同步固定版本 `08c3d7f999c9ea4cfd730ad3b608d5ef6bff4517`，包括同版本原生库与框架 shader。
168 个来源文件已逐字节核对，仅增加 `Libs/Foster/UPSTREAM.md` 来源记录；不在 vendored 副本直接维护定制。
桌面恢复完整 HDR/sRGB/mipmap 和线段拓扑。Foster 原生 CW 不变，Engine 的 MeshUpload3D 在上传边界适配 CCW；资产与 CPU 数据不用迁移。
Web 复用共享图形类型，移除临时五个覆盖副本，独立适配 ContentStorage、GPU 名称/调试标签和 wireframe 不支持行为。
DebugDraw3D 上传前清空缓冲计数，修复线段减少时残留绘制。

四条通用验收通过（Engine 3 项、实体/渲染 125 项），独立 WebDemo 构建通过；六组 shader 哈希通过。
D3D12/Vulkan 实际 GPU 回归通过：原生/适配绕序八组、静态/蒙皮加载与源索引不变、HDR 4、sRGB 解码、三层 mip/84 字节、最小 mip 实际采样 2.015625（期望 2，容差 0.03）、ACES 252、简单/CSM 阴影、减少后的调试线、关节 127 颜色与深度。
构建保留原有 Game0/测试字段与生成代码警告、Foster.Audio CA2022 警告。
浏览器/Metal 实际画面仍待目视验收：GltfModel 的 DamagedHelmet 材质，GltfScene 的 HDR/曝光和 CSM，Skinning 的混合与阴影。

本轮提交：DragonLib `e075592`，DragonLib.Tests `b4e8e98`，提交说明记录对应关系。
仅 Foster 的 MyFoster 分支推送到远程；DragonLib 和测试仓库保留本地提交。
原有 README 路径迁移和未跟踪文件保留，本计划继续保持未跟踪状态。

## 目视验收修复：DamagedHelmet 切换后空白（2026-10-04）

用户实测勾选头盔后只剩背景。实际资产五张贴图都是 JPEG，桌面 Foster 的 Image 仅解码 PNG/QOI；
真实 `.dasset` 加载复现 `Failed to decode PNG file`，扫描器跳过该资产，demo 把当前模型设为 null。
此前合成几何 smoke 未覆盖真实贴图加载，单纯确认资源存在不能证明可渲染。

修复放在离线 cook 和 demo：添加 StbImageSharp 2.30.16 解码 JPEG，使用 Foster 编码为 PNG；
PNG 原样保留，像素、行序与 alpha 不变，颜色/数据用途仍由运行时选择。没有修改 Foster 分支或 vendored 源码。
头盔已重 cook（v4，五张 PNG）；旧 JPEG `.dasset` 在桌面报出贴图/资产名称和重 cook 提示，格式布局不变。
demo 加载失败时保留当前模型、显示错误；切换当帧和返回场景采用对应取景参数。

添加 JPEG 像素保持、PNG 原样保留测试；GPU smoke 复制实际头盔资产，使用 demo 相机/光照渲染，
检查可见像素、材质变化和视锥计数，输出 `helmet-D3D12.png` / `helmet-Vulkan.png`。
四条通用验收通过，130 项 CPU 测试（3 + 127）；两驱动头盔读回均为 8422 个可见像素、零视锥剔除，图像检查通过。
DragonLib 提交 `98f01b9`，DragonLib.Tests 提交 `525c628`，仅本地提交。
用户需重新启动 Game0，再打开 glTF Model Demo 勾选 DamagedHelmet；浏览器/Metal 实际渲染仍未验证。
