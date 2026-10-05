# DragonLib

引擎库、第三方集成和工具链位于 `Libs` 与 `Tools`。引擎 API、桌面/Web 配置及依赖说明见 [Engine 文档](Libs/Engine/README.md)。

原 `Tests` 目录已拆到独立的 **DragonLib.Tests** 仓库，包含 Game0 Demo、编辑器、单元测试、字体渲染验证和性能基准。默认将该仓库与 DragonLib 放在同一父目录；它通过项目引用使用本仓库的库源码。

```powershell
dotnet build ../DragonLib.Tests/DragonLib.Tests.slnx
dotnet test ../DragonLib.Tests/DragonLib.Tests.slnx
dotnet run --project ../DragonLib.Tests/Game0/Game0.csproj
```

完整使用方式及自定义库路径配置见相邻仓库的 `README.md`。库的提交历史保留在本仓库，`Tests` 目录的提交历史也保留在拆出的仓库中。
