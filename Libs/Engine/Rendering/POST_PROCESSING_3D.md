# 3D 后处理

`PostProcessor3D` 是独立渲染 API，不依赖 ECS。Game0 示例已经迁移至相邻仓库 **DragonLib.Tests**，本仓库不创建 Game0；交互示例在 `../DragonLib.Tests/Game0/Content/Demos/GltfSceneDemo.cs`，有针对性的验证在 `../DragonLib.Tests/Post3D.Smoke`。

```csharp
// 初始化：后处理对象与图形设备同生命周期。
using var post = new PostProcessor3D(device)
{
    FxaaEnabled = true,
    BloomEnabled = true,
    BloomThreshold = 1f,
    BloomIntensity = .25f,
    SsaoEnabled = true,
    FogMode = FogMode3D.Linear,
    FogStart = 8,
    FogEnd = 30,
};

// 每帧：先完成场景渲染，再处理；最后才画 UI。
post.Exposure = 1f;
post.Composite(sceneRenderTarget, camera, batcher, screenWidth, screenHeight);
```

也可调用 `Process(scene, camera)` 得到最终颜色贴图，将它作为自定义合成的输入。返回值由 post 对象拥有，只能在下一次处理/重建目标/释放前使用；不要销毁返回贴图或拿它当输入目标回写。

执行顺序：

1. 从深度重建视空间位置，计算屏幕空间 AO，然后混合距离雾。
2. HDR 提取高亮，降到四分之一分辨率，进行横/纵 Gaussian 模糊并混回场景。
3. 曝光、ACES/Reinhard tonemapping 和 sRGB 编码。
4. 在 gamma 编码颜色上做 FXAA；保留 alpha，UI 不参与抗锯齿。

`FxaaEnabled` 默认开启，其他效果默认关闭。Bloom 需要 `RenderTarget3D.IsHdr` 为 true，LDR 路径直接绕过 Bloom 和 tonemapping；LDR 雾仍先解码成线性颜色再混合。`FogColor` 是线性颜色，支持 Linear/Exponential/ExponentialSquared；背景深度不加雾。所有中间目标按尺寸及源格式重建，支持缩放及 HDR/LDR 切换。

SSAO 无法补出屏幕外或透明物体未写入的深度，只使用深度导出的法线和 16 个视空间邻域样本；作为独立后处理近似，它乘到场景总颜色，不区分直接/间接光和自发光。使用 `SsaoRadius`（世界单位）、`SsaoStrength`、`SsaoBias` 调整；离屏/背景样本跳过，平面不自遮蔽。需要严格分离间接光时应使用单独的光照附件，当前阶段没有引入 G-buffer。

## 查看结果

```powershell
dotnet run --project ../DragonLib.Tests/Game0/Game0.csproj
# glTF Scene (ECS) → Post processing → FXAA / Bloom (HDR) / SSAO / Distance fog
# 这是现有示例的运行外壳，后处理库 API 本身无 ECS 依赖。
```

本阶段只新增一个针对后处理的实际 GPU 验证程序，不重复跑全量单元测试：

```powershell
dotnet run --project ../DragonLib.Tests/Post3D.Smoke/Post3D.Smoke.csproj
```

2026-10-06 Windows/D3D12 实测：FXAA 软化 254 个边缘像素，平坦颜色与 alpha 保持；Bloom 在高亮对象外读回 212（关闭时为 0）；距离 5、线性雾范围 0–10 的中心红色 206（计算值 205.9）；SSAO 使墙角 721 个像素变暗且平面不自遮蔽；HDR/LDR 切换及 3×2 重建通过。截图输出到验证程序的 `bin/Debug/net10.0`。Metal/Web 实际画面未实测，shader 四种产物随库生成。

算法背景：[NVIDIA FXAA](https://developer.download.nvidia.com/assets/gamedev/files/sdk/11/FXAA_WhitePaper.pdf)、[NVIDIA GPU Gems：Real-Time Glow](https://developer.nvidia.com/gpugems/gpugems/part-iv-image-processing/chapter-21-real-time-glow)。本实现使用本库的材质、采样和合成约定。
