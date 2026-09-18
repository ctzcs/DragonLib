# DragonLib 3D 能力增强计划（最终版，含 FBX）

## 原则

- Foster 层面 3D 基础设施完备（泛型 Mesh、Material 16 sampler 槽 + 8 uniform 槽、任意 shader），不做框架级改动。
- `Mesh3D` 是 demo 的临时设施，不改不扩展；新 3D 管线直接用 Foster `Mesh`。
- **引擎运行时只认 glTF**；FBX 等其它格式在导入期转成 glTF（离线工具链，零运行时依赖）。

## 缺口一：渲染入口放开顶点类型（很小）

- 新增 `PositionNormalUvVertex`（Engine\Rendering）：`Vector3 Position; Vector3 Normal; Vector2 Uv; Vector4 Tangent;`，location 0-3，Tangent.w 存 bitangent 符号（法线贴图预留）。
- `Renderer3D` 的 `Draw`/`DrawInstances` 参数从 `Mesh3D` 放宽为 Foster `Mesh`（`DrawItem.Mesh`、`EnsureMeshDevice` 同步改类型）；保留 `Draw(Mesh3D, …)` 转发重载，`ThreeDDemo` 零改动（作为回归验证）。
- 贴图绑定走 `Material.Fragment.Samplers`，Renderer3D 其余逻辑不动。

## 缺口二：glTF 模型管线（主体工作）

1. 封装层 `Libs\ThirdParty\Gltf\DragonLib.Gltf.csproj` + `Runtime\`，按 Box2D/Spine vendored 约定；SharpGLTF 源码 vendored 构建过重则封装层直接 NuGet 引用（影响面仅此项目）。
2. `GltfModelAsset`（IAsset）：mesh 列表 + 材质记录（albedo/normal 贴图的 `Texture`、baseColorFactor、metallic/roughness 数值透传）。
3. `GltfModelLoader`：.glb/.gltf → Foster `Mesh`（`Mesh<PositionNormalUvVertex, uint>`；读 glTF 的 NORMAL/TEXCOORD_0/TANGENT，缺省补默认值；模型无 tangent 时按 UV 梯度计算）；贴图从 glTF 图片解码为 Foster `Texture`。
4. `GltfModelScanner`：扫描 `Resources/Models/**` 注册 `AssetDatabase`（名字=相对路径去扩展名，.glb/.gltf 均收）。
5. `Standard3D.hlsl`（新 shader，compile.bat 编译嵌入 Game0）：UV 采样 albedo（无贴图绑 1x1 白纹理）；法线贴图 TBN 变换 + unpack，有/无贴图走 uniform 开关；保留 Basic3D 的方向光 lambert + ambient。

## 缺口三：ECS 3D 组件层 + 序列化打通

1. `Transform3DComp { Vector3 Position; Quaternion Rotation; Vector3 Scale; SpawnId? Parent; }` + `LocalToWorldComp { Matrix4x4 Value; }`（派生数据不进 prefab，照 PrefabRefComp 排除先例）。
2. `MeshRendererComp { AssetId Model; int MeshIndex; }`。
3. `Transform3DSystem`：拓扑序算 LocalToWorld，Parent 经 SpawnId→实体映射（复用 LevelSerializer.LevelRuntime 重绑机制）；`Render3DSystem`：组件 → `Renderer3D.Draw`。
4. 序列化天然支持（公开字段 struct + STJ 反射；AssetId 已有 JSON 支持；ComponentTypeCatalog 扫全程序集）——主要工作是新组件和系统，外加测试。
5. 验收：`Transform3DTests`（层级矩阵、prefab 差量往返）；GltfSceneDemo 摆带父子层级的模型，存 level json 重载一致。

## 缺口四：方向光 shadow map

1. `ShadowMap.cs`：深度 RT（`TextureFormat.Depth24`，查 `TextureSupports` 后备 Depth16/32）+ 光源正交 ViewProjection。
2. `DepthOnly.hlsl` 深度 pass shader；`Standard3D.hlsl` 加 `LightViewProjection` uniform + shadow-space 坐标 + PCF 3x3。
3. `Renderer3D` 加 `BeginShadowPass`/`EndShadowPass`（复用 DrawItem 列表，CullMode 翻转 Front 消 acne）。
4. `DirectionalLightComp` + ImGui 开关。
5. 验收：GltfSceneDemo 地面+模型投软阴影，可开关对比。

## 缺口五：FBX 导入工具链（离线转换，仿 ShaderCross 模式）

FBX 是 Autodesk 私有格式、无官方 .NET SDK，运行时直读不可靠；采用导入期转换：

1. `Tools\FbxToGltf\fbx_to_gltf.py` — Blender headless 转换脚本（fbx io 导入 + gltf 导出，保持贴图嵌入 .glb）。
2. `Tools\FbxToGltf\convert.bat`（+ `.sh`）— 仿 Shaders\compile.bat：遍历 `Resources/Models/**/*.fbx`，对每个比源文件新的 FBX 调 `blender --background --python fbx_to_gltf.py`，产物 .glb 输出到源文件同目录；Blender 路径取 `BLENDER` 环境变量或 PATH，未安装时报清晰提示。
3. `GltfModelScanner` 天然拾取转换产物（缺口二已覆盖），引擎侧零改动。
4. `Tools\FbxToGltf\README.md`：说明需安装 Blender、运行时机（放入 FBX 后手动跑一次/可挂 pre-build）。
5. 验收：放一个 .fbx 测试文件进 Resources/Models，跑 convert.bat 生成 .glb，GltfSceneDemo 能加载渲染。
6. 备注：若将来确需运行时直读 FBX/多格式（OBJ、DAE 等），可再评估 AssimpNet（原生 assimp.dll）——本期不做，避免引入运行时原生依赖。

## 顺序与验证

一→二→（三、四、五 并行度可控但按序做）→ 每步 `dotnet build` + `dotnet test` 通过后独立 commit。风险点：SharpGLTF 构建重量（NuGet 后备）；Foster 深度纹理采样支持面（先写小验证再展开）；Blender 未安装时 convert.bat 的降级提示。