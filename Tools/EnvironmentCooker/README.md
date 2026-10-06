# EnvironmentCooker

离线读取 Radiance HDR 或 sRGB 全景图，重采样为线性 RGB，积分漫反射/GGX roughness atlas/BRDF LUT，写出 Engine 可直接上传的 `.denv`。

```powershell
dotnet run --project Tools/EnvironmentCooker/EnvironmentCooker.csproj -- input.hdr output.denv 128 64 128
dotnet run --project Tools/EnvironmentCooker/EnvironmentCooker.csproj -- --sky output.denv
```

后续参数依次为宽、高、积分样本数；默认 64×32、128 样本。不是单张普通照片到全景的转换器，输入应是经纬度环境图。不支持 EXR。StbImageSharp 仅用于离线解码，运行时 Engine 没有该包依赖。

示例资产与 Game0 放在独立 `../DragonLib.Tests`。完整用法见 [Environment 文档](../../Libs/Engine/Rendering/ENVIRONMENT_3D.md)。
