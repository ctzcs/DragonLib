# DragonLib Prowl 源码

本目录维护来自 [ProwlEngine/Anthology 3.6.7](https://github.com/ProwlEngine/Anthology/tree/5df3a74f8a4d2357f8d6a1dbef1e72fc0fbf46e0)
的源码，固定提交 `5df3a74f8a4d2357f8d6a1dbef1e72fc0fbf46e0`。
普通 clone 即包含源码，无需子模块或单独下载。来源、目录映射与本地差异见 [UPSTREAM.md](UPSTREAM.md)。

## 已接入的 UI 依赖

```text
Engine → Origami → Paper → Quill → Scribe → Vector
           ↓         ├→ Scaffold
          Echo       └→ Quire
```

Engine 也直接引用 Paper/Quill。以上依赖全部通过本地 `ProjectReference` 构建，
不再混用 Prowl 的 NuGet 程序集。Foster 渲染、输入、精灵适配仍在 `Libs/Engine/Paper`。
Echo 的源码生成器作为构建依赖保留；这次更新没有替换 DragonLib 的 JSON/`.dasset` 序列化。

## 已拉取、待按需接入的模块

| 目录 / 项目 | 用途 | 当前状态 |
| --- | --- | --- |
| `Clay/Clay.csproj` | FBX、OBJ、glTF 模型导入 | 已构建；尚未接入 `.dasset` cooker |
| `Motion/Motion.csproj` | 动画图、IK、重定向 | 已构建；尚未替换 SkeletonAnimator |
| `Recast/Prowl.Recast.csproj` | NavMesh、寻路和群体避障 | 已构建；尚未接入游戏 / ECS |
| `Aperture/Aperture.csproj` | 多格式图片解码 | 已构建；尚未替换图片导入流程 |
| `Rosetta/Rosetta.csproj` | 多语言 | 已构建；尚未添加 GameStorage/Luban provider |
| `Unwrapper/Unwrapper.csproj` | UV 展开 | 已构建；尚未接入模型处理 |
| `Photonic/Photonic.csproj` | CPU 光照烘焙 | 已构建；尚未添加 Lightmap 运行时支持 |
| `Ember/Ember/Prowl.Ember.csproj` | 热重载对象状态迁移 | 已构建；仅供后续编辑器集成评估 |

这些可选模块没有添加到 Engine 的运行时依赖中。Ember 的 Contracts/Analyzers 一并保留，
其运行时依赖 Mono.Cecil；分析器和 Echo 生成器的 Roslyn 依赖用于各自构建/工具用途。
Graphite、Slang、Crumb 未引入。

## DragonLib 定制

- Scribe 默认使用动态 MSDF，也可显式选择旧 SDF；保留原构造函数。
- Quill 的 `FontAtlasSettings` 暴露 `DistanceFieldMode` 和 `DistanceRange`（默认 4 图集像素）。
- `MsdfGlyphGenerator` 使用 Scribe 字形轮廓调用原生 msdfgen；整数位图范围与字形区域严格一致。
- 字形生成后禁止改变距离范围，防止同一图集混用尺度。3.6.7 新增的下划线/删除线距离场，
  在生成字形前修改范围时会重新生成。
- Foster 桥接适配 Quill 的 Span 几何数据和包含 DPI 的仿射裁剪接口。

字体度量、换行、光标、缓存与控件逻辑继续使用上游实现。原生库构建和其他平台部署见
[Msdfgen](../Msdfgen/README.md)。Foster 使用自身已编译 HLSL shader，关闭上游 Quill 的
Slang shader 自动生成；修改 Foster shader 时运行 `Libs/Engine/Paper/Shaders/build.ps1`。

## 构建与升级

从 2.7.0 升级的调用方需要注意：`Easing` 位于 `Prowl.Vector`；富文本改为
`.Text(text, font).RichText(bold, italic, boldItalic, mono)`，标签使用 `<b>粗体</>`、
`<#66ccff>彩色</>` 等语法；自定义 Quill 后端的几何数据改为 Span，裁剪接口也已变化。
相邻 DragonLib.Tests 的 Game0 与字体示例已同步这些调用。

`Directory.Build.props` 统一版本及 `net10.0` 目标，Engine 的桌面/browser 目标均可引用。
Roslyn 生成器/分析器保留 `netstandard2.0`。禁止自动打包发布，避免误发布上游包名。

```powershell
# 在 DragonLib 根目录运行
dotnet build Libs/ThirdParty/Prowl/Prowl.slnx
dotnet build Libs/Engine/Engine.csproj
dotnet test ../DragonLib.Tests/Paper.Msdf.Tests/Paper.Msdf.Tests.csproj
dotnet build ../DragonLib.Tests/DragonLib.Tests.slnx
dotnet run --project ../DragonLib.Tests/Paper.Msdf.Smoke -- .codex-build/paper-msdf.png
dotnet run --project ../DragonLib.Tests/Paper.Msdf.Smoke -- .codex-build/paper-msdf-2x.png 2
```

后续可直接修改这些源码；同步上游时用固定提交做三方比较，保留上述定制与许可证。
优先独立评估可选模块，源码到位并不代表其功能已经在 Engine 开启。
