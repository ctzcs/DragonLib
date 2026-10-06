# 聚光灯与 LOD（阶段 9）

API 不依赖 ECS，不修改 Foster。示例及验证程序均在同级 DragonLib.Tests。

## 聚光灯

```csharp
lighting.SpotLights.Add(new SpotLight3D(position, direction, range: 20,
    color: Vector3.One, intensity: 3, innerAngle: .2f, outerAngle: .45f));
renderer.Begin(target.Target, camera);
renderer.SetLighting(lighting);
renderer.DrawModel(model, materials, world);
renderer.End();
```

Direction 是灯向外照射的方向，角度为弧度半角。内锥到外锥 smoothstep 衰减；距离衰减与点光一致，range 外为零。聚光灯不投影阴影。零方向使用 -Y；角度限制到 [0, π/2]，inner 不超过 outer；负 range/intensity/color 打包时归零。

标准 fragment b3 保留前 528 字节点光布局，后面新增 1040 字节聚光灯数据，总共 1568 字节，低于 SDL Vulkan 的 4096 字节 uniform 限制。每种灯最多 16 个，按列表顺序截断，未做 clustered lighting。自定义标准 shader 需同步 b3 布局；LightSandbox 使用的 PointLight3D.Pack 保持不变。

在 DragonLib.Tests/Game0 的 glTF Model Demo 勾选 Camera spotlight，调低 Ambient/Diffuse intensity 更容易看到光锥。

## 模型 LOD

```csharp
// 每个实例/相机持有独立 LOD 状态；模型资源由加载者拥有。
var lod = new ModelLod3D((highModel, .3f), (mediumModel, .1f), (lowModel, 0));
// 每帧先设置 camera.ViewportSize 为 target 尺寸。
var selected = lod.Select(camera, world);
renderer.DrawModel(selected, materialCache.Get(selected, shaders), world);
```

阈值为保守包围球的投影直径 / 视口高度，严格从大到小排列，最后为 0。LodSelector3D 也可单独用于自建 Mesh。默认 10% 滞回：缩小时低于阈值的 90% 才降低细节，放大时超过 110% 才提高细节。包围球与近裁面相交时选择最高细节；旋转及非均匀缩放通过世界 AABB 参与尺寸估算。

负责选择已有模型，不自动简化网格，不增加资产版本。动画模型应提供共同的保守动画 Bounds；不同级别的骨架/palette 必须由调用者保证兼容。

## 可选遮挡判断

renderer.OcclusionCuller 接受 IOcclusionCuller3D。BeginFrame(camera) 每个颜色 pass 调用一次，随后在视锥测试之后对有 bounds 的 draw 调用 IsOccluded(worldBounds)。true 跳过颜色 draw 并增加 LastFrameOccludedCount；阴影 pass 保留。默认 null，无 bounds 的 draw 保守提交。

这是接入接口，**没有内置 GPU occlusion query / Hi-Z**，不自动读回深度。实现者只在确认整个 AABB 被遮挡时返回 true；结果未就绪、相机或遮挡物改变导致旧结果失效时返回 false。不要直接用延迟的上一帧结果删除当前帧物体。

## 必要验证

Visibility3D.Smoke 一次 D3D12 运行：中心光照 1.232、锥外为 0，翻转方向或超出 range 后为 0；可选遮挡跳过 draw，关闭后恢复；LOD 投影尺寸和滞回通过。四后端 shader 及哈希已生成，桌面/Web 构建通过。
