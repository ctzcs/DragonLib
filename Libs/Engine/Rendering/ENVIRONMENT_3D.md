# 天空与 IBL

独立于 ECS 的环境光 API：`EnvironmentBake3D` 做纯 CPU 离线预计算，`EnvironmentMap3D` 上传贴图，`SkyRenderer3D` 画天空，`SceneLighting3D.EnvironmentMap` 让静态/蒙皮 PBR 材质接受 IBL。

使用普通 **2D 经纬度贴图**：漫反射 cos 加权卷积、GGX roughness 层的 atlas、split-sum BRDF LUT。没有修改 Foster 或要求 cubemap/纹理数组；各 atlas 层独立补边、按 roughness 在相邻层插值。环境像素为线性 RGB，可以大于 1；支持 RGBA16F，缺少该格式时回退线性 Color 并截断大于 1 的亮度。

## 离线 cook

```powershell
# HDR/PNG 等输入必须是经纬度全景；LDR 显式按 sRGB 解码，Radiance HDR 保留线性亮度。
dotnet run --project Tools/EnvironmentCooker/EnvironmentCooker.csproj -- room.hdr ../DragonLib.Tests/Game0/Resources/Environment/Room.denv 128 64 128

# 不需要下载外部资源的程序天空；实际示例资源已写在独立 DragonLib.Tests 仓库。
dotnet run --project Tools/EnvironmentCooker/EnvironmentCooker.csproj -- --sky ../DragonLib.Tests/Game0/Resources/Environment/DefaultSky.denv
```

参数：输入/输出、宽度（默认 64）、高度（默认 32）、积分样本数（默认 128）。采样数和分辨率决定离线耗时/质量，运行时不进行积分。运行时 Engine 不依赖 StbImageSharp，解码库只加入离线工具。

`EnvironmentBake3D.Bake(linearRgb, width, height, levels, samples, lutSize)` 也接受应用自己的浮点 RGB。`.denv` v1 保存源图、漫反射、GGX atlas 和 BRDF LUT；`Read/Write(Stream)` 保持调用者拥有流，读取时检查头、尺寸、像素预算、截断及非有限/负辐射值。

## 渲染

```csharp
using var input = storage.OpenRead("Resources/Environment/Room.denv");
using var environment = new EnvironmentMap3D(device, EnvironmentBake3D.Read(input));
using var sky = new SkyRenderer3D(device);

// 每帧：先 Clear；天空不写深度，几何随后遮挡它。
target.HdrEnabled = true;
target.Resize(width, height);
target.Clear(Color.Black);
sky.Draw(target, camera, environment, intensity: 1, rotation: 0);

lighting.HdrEnabled = target.IsHdr;
lighting.AmbientColor = Vector3.Zero; // 不想叠加常量环境光时关闭旧 Ambient。
lighting.EnvironmentMap = environment;
lighting.EnvironmentIntensity = 1;
lighting.EnvironmentRotation = 0; // 弧度；天空与 IBL 使用相同角度。
renderer.Begin(target.Target, camera);
renderer.SetLighting(lighting);
// Draw / DrawModel …
renderer.End();
post.Composite(target, camera, batcher, width, height);
```

HDR/线性路径推荐用于 IBL。LDR 保留旧的直接光照观感，将新增间接项编码后相加；它不是完整的物理线性管线。材质 AO 衰减环境的漫反射/镜面，不影响直接光和自发光。`EnvironmentMap` 设为 null 可关闭 IBL；地图由调用者拥有，世界退出后统一 Dispose，关闭后的材质绑定会替换为有效默认贴图。

Standard3D 片元采样器由 6 增到 9（新增 diffuse/specular/BRDF 的槽 6/7/8），每张贴图使用独立 sampler。fragment b0 的 `LightUniforms` 由 80 增到 112 字节，环境参数附在末尾；保持四个片元 uniform 槽及 palette 两个 4KB 块不变。自定义材质应使用新的 `Standard3DShaders.FragmentSpec` 和布局；`.dasset` 格式未改变。

## 查看与验证

启动相邻仓库的 Game0，打开 **glTF Model Demo**，勾选 **DamagedHelmet**、**Image-based lighting**、**Sky environment**，可调整强度与旋转。开启 IBL 时自动启用 HDR；如需只看环境光，把 Diffuse/Ambient intensity 都设为 0。Game0 和资源只位于 `../DragonLib.Tests`。

本阶段只做针对性验证：`Environment3D.Smoke` 检查均匀环境的积分保持、`.denv` 往返、实际 HDR 天空、split-sum 金属反射、关闭已释放地图，以及真实头盔渲染；不重复运行全量测试。Windows/D3D12 金属蓝色读回 1.990（关闭为 0），均匀天空蓝色为 2，真实头盔对比天空的变化像素 8643，输出 `helmet-ibl.png`。Web/Metal shader 和构建检查不等于实际画面验证。

理论参考：[Filament 的物理渲染与 IBL 说明](https://google.github.io/filament/Filament.md.html)。这里采用基础 split-sum，不包含多次散射补偿、局部探针或视差校正。
