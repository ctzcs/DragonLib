# 3D 扩展使用

后续按独立阶段推进，见 [ROADMAP_3D.md](ROADMAP_3D.md)。后处理 API 使用与验证见 [POST_PROCESSING_3D.md](POST_PROCESSING_3D.md)；不依赖 ECS，示例仅写在相邻 DragonLib.Tests 仓库。

天空、离线环境预计算和 IBL 见 [ENVIRONMENT_3D.md](ENVIRONMENT_3D.md)，使用普通 2D 经纬度贴图，不要求修改 Foster。

聚光灯、LOD 与可选遮挡接口见 [VISIBILITY_LIGHTS_3D.md](VISIBILITY_LIGHTS_3D.md)。

独立动画事件、状态机、根运动和 CCD IK 见 [ANIMATION_3D.md](../Animation/ANIMATION_3D.md)。

标准管线由 `Standard3DShaders` 拥有 shader 和默认白贴图，`MaterialCache` 由模型装配材质。每帧 `Renderer3D.Begin` 后设置 `SceneLighting3D`，再排队 draw。共享材质在每次提交前更新光照；模型卸载时清空缓存。

`CascadedShadowMap` 使用四级 2×2 深度 atlas。先设置相机 viewport，再 `Update(camera, direction)` 和 `Clear()`；把同一对象放入 `SceneLighting3D.Cascades`，并调用 `Renderer3D.SetShadowPass(cascades, shaders.Depth, shaders.SkinnedDepth)`。`MaxDistance` 限制覆盖距离，`Lambda` 混合线性/对数分段，`BlendFraction` 控制重叠，`DebugColors` 显示级联。简单 `ShadowMap` 路径仍可单独使用。两者采样都把 NDC 向上 y 转为纹理向下 y；PCF 被限制在当前 tile 内。

实例阴影需要在 `DrawInstances` 传入 `instancedDepthMaterial`：顶点 b0 是光源 ViewProjection，实例缓冲布局与颜色 shader 相同，顶点变换和动画也必须一致。未提供时不会投影；通用管线无法推断自定义实例动画。阴影 pass 使用 Cull.Front，不做相机视锥剔除。四级矩阵等设置共用 fragment b2，未增加 SDL uniform 槽位。

检查 GltfScene 的 Cascaded shadows / Cascade colors 开关；GltfModel 的 DamagedHelmet 开关检查 MR、AO、自发光。`Rendering3D.Smoke` 在 D3D12/Vulkan 实际读回验证 MyFoster 原生 CW、Engine 索引上传适配、HDR/sRGB/mipmap，以及非对称遮挡物在简单阴影/CSM 中的上下投影方向。

glTF 的 JPEG 贴图在 cook 阶段转 PNG，桌面 Foster 原生不解码 JPEG。旧 JPEG `.dasset` 需重 cook。
DamagedHelmet 资源已更新，GPU smoke 加载实际资产并以 demo 相机读回；在输出目录生成 `helmet-D3D12.png` / `helmet-Vulkan.png`。
demo 加载失败时保留当前模型并显示错误，模型切换和返回场景都会保持对应取景。

`Camera3D.ScreenPointToRay(pixel)` 接收绘制目标的像素坐标，原点在 near 平面，方向已归一化。`Intersections3D` 返回世界单位距离，AABB/球内起点返回 0，三角形支持双面，平面用点和法线定义。逐三角形拾取用 `Primitive`，蒙皮可传当前 palette；`DassetModelLoader.Load(..., retainCpuGeometry: true)` 才会保留 `CpuGeometry`。

`DebugDraw3D` 提供 Line/Aabb/Sphere/Frustum/Axis/Grid/Skeleton；排队后 `Render(target, camera)` 会清空。`DepthTestEnabled` 控制遮挡，始终不写深度。Skeleton 默认接收 palette，通过 inverse IBM 重建 globals；也可传 `isPalette: false`。GltfScene 左键选择实体并显示 AABB，Skeletons / Light ranges 开关显示调试线。

调试线使用 MyFoster/Web 的线段拓扑，顶点缓冲在每次上传前清空计数。
桌面定制先在独立 MyFoster 分支验证并推送，再同步固定版本；版本与上游差异见 Foster/UPSTREAM.md。

`.dasset` 当前写 v4，读取兼容 v1–v4。v3 扩展材质，v4 增加动画插值和 cubic 入/出切线；旧 channel 默认 Linear。`AnimatorComp.CrossFadeTo(index, duration)` 重置目标时间，`AnimationSystem` 推进两个剪辑并做 TRS 混合，完成后延续目标播放时间；仅支持同一骨架的过渡。

关节上限 128，palette 分 vertex b2/b3 两块各 4KB（SDL 3.4 Vulkan 每槽 range 只有 4KB）；颜色/阴影 shader 对旧资产越界下标回退关节 0。蒙皮 draw 按当前 palette 变换 bind AABB 后求联合盒：非负归一化权重的顶点是这些点的凸组合，因此盒子保守覆盖当前姿态和剪辑混合；每 draw 约 8×关节数个角点变换，可能比实际姿态宽。Skinning 的 Blend wave / rest 按钮检查过渡和动画阴影。
