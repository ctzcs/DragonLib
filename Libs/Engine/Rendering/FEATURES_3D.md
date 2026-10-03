# 3D 扩展使用

标准管线由 `Standard3DShaders` 拥有 shader 和默认白贴图，`MaterialCache` 由模型装配材质。每帧 `Renderer3D.Begin` 后设置 `SceneLighting3D`，再排队 draw。共享材质在每次提交前更新光照；模型卸载时清空缓存。

`CascadedShadowMap` 使用四级 2×2 深度 atlas。先设置相机 viewport，再 `Update(camera, direction)` 和 `Clear()`；把同一对象放入 `SceneLighting3D.Cascades`，并调用 `Renderer3D.SetShadowPass(cascades, shaders.Depth, shaders.SkinnedDepth)`。`MaxDistance` 限制覆盖距离，`Lambda` 混合线性/对数分段，`BlendFraction` 控制重叠，`DebugColors` 显示级联。简单 `ShadowMap` 路径仍可单独使用。两者采样都把 NDC 向上 y 转为纹理向下 y；PCF 被限制在当前 tile 内。

实例阴影需要在 `DrawInstances` 传入 `instancedDepthMaterial`：顶点 b0 是光源 ViewProjection，实例缓冲布局与颜色 shader 相同，顶点变换和动画也必须一致。未提供时不会投影；通用管线无法推断自定义实例动画。阴影 pass 使用 Cull.Front，不做相机视锥剔除。四级矩阵等设置共用 fragment b2，未增加 SDL uniform 槽位。

检查 GltfScene 的 Cascaded shadows / Cascade colors 开关；GltfModel 的 DamagedHelmet 开关检查 MR、AO、自发光。`Rendering3D.Smoke` 在 D3D12/Vulkan 实际读回验证 CCW、HDR，以及非对称遮挡物在简单阴影/CSM 中的上下投影方向。

`Camera3D.ScreenPointToRay(pixel)` 接收绘制目标的像素坐标，原点在 near 平面，方向已归一化。`Intersections3D` 返回世界单位距离，AABB/球内起点返回 0，三角形支持双面，平面用点和法线定义。逐三角形拾取用 `Primitive`，蒙皮可传当前 palette；`DassetModelLoader.Load(..., retainCpuGeometry: true)` 才会保留 `CpuGeometry`。

`DebugDraw3D` 提供 Line/Aabb/Sphere/Frustum/Axis/Grid/Skeleton；排队后 `Render(target, camera)` 会清空。`DepthTestEnabled` 控制遮挡，始终不写深度。Skeleton 默认接收 palette，通过 inverse IBM 重建 globals；也可传 `isPalette: false`。GltfScene 左键选择实体并显示 AABB，Skeletons / Light ranges 开关显示调试线。
