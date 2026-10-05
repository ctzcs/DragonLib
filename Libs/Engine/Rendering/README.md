# Engine.Rendering 渲染约定

3D 渲染管线横跨「资产 cook → .dasset → 运行时 → ECS → shader → SDL_GPU 后端」六层，
任何一层的约定错位都会以「内外颠倒 / 光照反向 / 跟相机转」的形式爆发，且每层单看都自洽。
本文件是唯一权威约定来源；改动任一层的相关代码前，先核对这里。**约定变更必须同步更新本文件与锁定测试。**

Foster 是第三方库，优先遵循其已有行为，兼容处理放在 Engine。
确需框架改动时，在独立 Foster 仓库的 `MyFoster` 分支实现、验证并推送到 `origin/MyFoster`，
随后同步固定提交到 `Libs/Foster`，记录上游基线和定制提交 SHA；不要直接在 vendored 副本维护定制改动。
该仓库的 `origin` 为 `ctzcs/Foster`，`upstream` 为 `FosterFramework/Foster`。
上游更新先在独立仓库获取并合入定制分支，再用上游与定制分支的差异追踪扩展。
自有 `Libs/Foster.Web` 可扩展，并用独立覆盖文件复用第三方通用源码。
当前固定版本和定制来源见 [Foster/UPSTREAM.md](../../Foster/UPSTREAM.md)，原生正面设置保持 CW。

## 正面绕序（front face）

**优先遵循 Foster 框架的既有设置，在 Engine 层适配。桌面 GPU 使用 Foster 默认的 CW 正面。**

glTF、`.dasset` 和 CPU 几何仍采用从外侧看 CCW 的索引，法线朝外。统一经
`MeshUpload3D.SetTriangleIndices` 上传：桌面交换每个三角形的后两个索引，Web 保留原序。
`Mesh3D` 构造、Dasset 静态/蒙皮加载都使用此入口；自建 Foster Mesh 也应使用它。
转换只发生在 GPU 上传时，不修改源数组或模型文件，拾取和资产测试仍使用 CCW 数据。

后端设置与验证：

- SDL_GPU 保留上游的 `SDL_GPU_FRONTFACE_CLOCKWISE`，`CullMode.Back/Front` 直接映射。
  不修改 Foster 的 front-face 或交换 CullMode；颜色、阴影及实例化共用上传后的索引。
- 原生 CCW 索引直接提交到桌面后端时，Back 剔除它、Front 保留它；原生 CW 则相反。
  “像素坐标 y 向下，所以 CW 等于 CCW”的旧解释不符合实际结果。
- `../DragonLib.Tests/Rendering3D.Smoke` 使用叉积朝 +Z、相机从 +Z 观察的固定三角形，
  分别对照原生提交和 Engine 上传的 CCW/CW × Back/Front 组合，并验证 Dasset 静态/蒙皮上传。
  适配后，CCW 源几何在 Back 下可见、Front 下不可见；CPU 源索引保持不变。
- WebGL 的离屏 shader 会乘 `u_target_flip=-1`，所以离屏目标使用 `gl.CW`、屏幕使用 `gl.CCW`；
  沿用这组配套设置，Engine 在 Web 上传时保留 CCW，避免重复翻转。

推论：

- 镜像矩阵（负行列式）会反转绕序，cook 时**仅此时**翻一次补偿
  （`GltfModelCooker` 的 `flipWinding = determinant < 0`）。
- GPU CW 索引的叉积与朝外法线反向是绕序转换的结果；不能用它重算法线或切线。
  资产层的几何真值仍由 `../DragonLib.Tests/Game0/Tests/WindingTests.cs` 锁定。

## 矩阵约定

- **行向量**：`v' = v × M`（`Vector3.Transform`）；「先 A 后 B」写作 `A × B`
  （如 `World × ViewProjection`、`local × parentWorld`、`IBM × jointGlobal`）。
- 内存布局 = System.Numerics 的 `M11..M44` 行主序字段连续排列；cbuffer 上传原样字节。
- shader 侧 `mul(M, v)` 在本管线等价于 C# 的 `v × M`（shadercross/DXC 的 cbuffer 布局使然，
  已被全部在位 shader 实证；新增矩阵 cbuffer 沿用同一形态即可，不要自行 transpose）。
- 蒙皮 palette：`palette[i] = IBM[i] × global[i]`（先 IBM 把顶点从 mesh bind 空间带进关节局部，
  再乘关节全局矩阵回骨架空间）；实体世界矩阵照常乘在蒙皮结果之后。bind pose 下 palette ≈ 恒等。
  锁定测试：`SkinningTests.BindPosePaletteIsIdentityAtOriginAttachment` 与
  `WindingTests.AnimatedJointGlobalsMatchSharpGltfOfficialEvaluation`（与 SharpGLTF 官方求值对照）。

## 法线变换

- **静态路径（cooker）**：逆转置矩阵（`transpose(inverse(world))`）变换法线/切线，
  非均匀缩放下保持正确；镜像矩阵下法线随之镜像，与补偿后的绕序保持一致
  （锁定：`WindingTests.MirroredNodeTrianglesStillFaceOutward`）。
- **蒙皮路径（shader）**：`normal × skin3×3 × world3×3` 直乘后归一化。
  适用条件：蒙皮矩阵以旋转为主（LBS 加权和）。已知限制：关节含非均匀缩放/剪切时法线方向有偏差
  （本期蒙皮不做法线逆转置——逐矩阵求逆代价不值得；出现真实需求再升级）。
- 片元侧始终 `normalize(input.Normal)`（插值后长度≠1）。

## 阴影 pass

- 方向光阴影用 **front-face culling**（`CullMode.Front`）：剔除正面、用背面（内壁）深度写深度图，
  消除自遮挡 acne（`Renderer3D.SubmitShadow`）。
- 阴影 pass **不做相机视锥剔除**：屏外物体仍可能把阴影投进画面。
- 实例化 draw 不进阴影 pass：实例缓冲布局由调用侧 shader 自定义，且实例顶点动画只存在于颜色 pass，
  通用深度变体画出的剪影是错的。
- 蒙皮 draw 在 `SetShadowPass` 提供 `skinnedDepthMaterial` 时进入阴影 pass：
  Engine 内置 `DepthOnlySkinned` 读取与颜色 pass 相同的 vertex slot 2 palette。未提供变体时跳过。
  关节 palette 上传完整 cbuffer，未使用的关节填 identity；深度顶点关节属性固定 location 4/5，避免后端压缩槽位。

## 透明与双面材质

- `DassetMaterial.AlphaMode` 三态：Opaque / Mask / Blend。
  - Opaque、Mask 走不透明队列（Mask 由 shader 按 `AlphaCutoff` clip，正常写深度）；
  - Blend 走透明队列：back-to-front 排序、`DepthWrite=false`、`CullMode` 默认 Back。
  - `DoubleSided=true` → `CullMode.None`（两面都画）。**内壁/背面会用朝外的法线计算光照，
    明暗必然不对——这是双面材质的固有表现，不是 bug。** 正确做法是让资产成为封闭单面几何。
- 开口薄壳（无盖管子等）在单面剔除下能看穿到背景，观感像「内外不分」——资产层面封盖解决
  （教训见下）。

## 出错史与教训

1. **资产数据与后端绕序混淆**：转换只在上传边界做一次，法线和切线保持原值。
   教训：约定横跨多层时，**必须有「几何真值测试」**——断言相对**绝对参照物**
   （如「三角形叉积背离柱体中轴」「叉积与资产的顶点法线同向」），而不是层内自洽测试
   （「叉积与顶点法线一致」在法线也错时会双双通过）。
2. **蒙皮示例模型是开口管**：几何验证只查了侧面，漏了「管子没有盖」这一几何事实；
   修复绕序后仍能看穿管腔。教训：验证渲染问题的归因前，先用真值测试区分
   「约定错 / 数据错 / 几何本身如此 / 光照几何的正确行为」。
3. **「顶盖亮、侧面全黑」**：实为光照几何（方向光斜向下，背光侧面只剩环境光）+ 掠射视角的正确结果，
   非法线 bug。教训：光照类观感问题先算 nDotL 数值表再动代码。
   锁定：`SkinningLightingTests.EachFaceBrightnessMatchesLightDirectionGeometry`。

## 相机 orbit 方向约定

demo 的轨道相机（SkinningDemo/GltfSceneDemo/GltfModelDemo/LightSandboxDemo/ThreeDDemo）统一：
**向右拖（Mouse.Delta.X > 0）→ yaw += delta → 相机向右绕场景（看到物体右侧面）→
固定世界点屏幕 x 左移**（与 Unity/Blender 的 orbit 惯例一致；写反了用户会体感「模型在跟手转」）。
锁定测试：`CameraOrbitTests.DragRightOrbitsCameraRightAndFixedPointMovesLeft`。

另注意：近沿柱排/队列轴线观察时，微小方位角变化会让「重叠」与「散开」剧烈切换
（透视特性，非 bug）；无参照物的纯色背景会放大「谁在动」的误判——3D demo 场景宜放静止地面网格。
锁定测试：`CameraOrbitTests.OrbitParallaxMatchesUserScreenshots`。

## 相关文档

- 资产管线/格式：[`Libs/Engine/Assets/README.md`](../Assets/README.md)
- FBX→glTF→dasset cook 工具链：[`Tools/FbxToGltf/README.md`](../../../Tools/FbxToGltf/README.md)

## 标准 3D 管线

`Standard3DShaders` 持有 Engine 内嵌的静态、蒙皮与两种深度 shader，以及白色默认贴图。
`MaterialCache.Get(model, shaders)` 按模型对象缓存逐 primitive 材质；卸载/热重载模型时清空缓存。
游戏在 `Renderer3D.Begin` 后调用 `SetLighting(SceneLighting3D)`，需要阴影时再调用
`SetShadowPass(shadow.Target, shaders.Depth, shadow.LightViewProjection, shaders.SkinnedDepth)`。
`DrawModel` 统一处理材质、蒙皮 palette、primitive 选择和局部包围盒；无 palette 时使用 bind pose。
ECS 游戏可用 `MeshRenderSystem.Submit` 排队，Begin/End 和目标的生命周期仍由游戏管理。
方向光和点光配置可以随 level/prefab 序列化，`SceneLight3DCollector` 收集到光照对象。

shader 源码位于 `Rendering/Shaders/`，运行 `build.ps1` 生成四种后端产物。
`Verify-Shaders.ps1` 同时校验入口、递归 include 依赖和产物，修改公共片元逻辑后必须重新生成。

## 色彩管线

桌面 MyFoster 与 Web 均提供浮点颜色、硬件 sRGB 和 mipmap 扩展。
`RenderTarget3D.HdrEnabled` 请求 RGBA16F；`IsHdr` 反映实际是否启用，缺失支持时回退 Color。
`SceneLighting3D.HdrEnabled` 应使用目标的 `IsHdr`：HDR 计算线性光照，LDR 保留原有 gamma 光照观感。
模型颜色贴图使用 sRGB，数据贴图使用线性格式，共用源图时分别上传；模型贴图生成 mip 链并使用 mip sampler。
`DebugDraw3D` 使用 MyFoster/Web 的线段拓扑，逐帧清空顶点缓冲计数，避免线段减少时绘制残留几何。
HDR 合成需传入 `Tonemapper3D`，支持 ACES/Reinhard 和曝光；普通屏幕输出显式 sRGB 编码。
合成到 sRGB 颜色附件时传 `outputSrgb:false`，避免重复编码。原 LDR Batcher 合成路径保留。
后端调查及限制见 `COLOR_PIPELINE_FEASIBILITY.md`。

## 扩展接口

材质贴图、HDR、CSM atlas、拾取、DebugDraw3D 和动画过渡见 [FEATURES_3D.md](FEATURES_3D.md)。当前 palette 上限为 128（vertex b2/b3 各 4KB）；蒙皮颜色 pass 用当前 palette 的保守联合 AABB 剔除。D3D12/Vulkan smoke 锁定 Foster 原生 CW、Engine 上传适配、阴影采样 y 方向及第 127 号关节的颜色/深度变换。
