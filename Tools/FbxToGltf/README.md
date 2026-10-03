# FbxToGltf — 模型资产烘焙管线

DragonLib 运行时**只加载 `.dasset`**（引擎私有二进制，见 `Libs/Engine/Assets/Dasset`）。
完整管线是两级离线转换：

```
fbx →(Blender, convert)→ .glb →(DassetCompiler, cook)→ .dasset → 运行时二进制直读
```

- FBX 是 Autodesk 私有格式、没有官方 .NET SDK，按业界惯例在导入期转成 glTF；
- SharpGLTF 不进运行时热路径：`.glb` 再被 cook 成 `.dasset`（顶点/索引整块二进制 +
  贴图 PNG 字节 + 材质参数 + AABB），运行时 `DassetModelScanner` 直读上传 GPU。
  输入 JPEG 在 cook 阶段解码转 PNG，匹配桌面 Foster 的 PNG/QOI 解码能力。

两级产物都放在源文件同目录，`DassetModelScanner` 启动时会自动注册（资产名 =
相对 `Resources` 的路径去扩展名，如 `Models/character`）。

## 用法

一条命令跑全链（先 convert 再 cook，各自增量跳过）：

```bash
# 任选一种方式让脚本找到 Blender：
#   1) Blender 在 PATH 里
#   2) 设置环境变量 BLENDER 指向 blender.exe
#   3) 直接传路径（Windows 会额外探测 Program Files\Blender Foundation\*\blender.exe）

Tools/FbxToGltf/cook.bat                        # Windows
Tools/FbxToGltf/cook.bat "C:\...\blender.exe"   # Windows，显式路径
Tools/FbxToGltf/cook.sh                         # Linux/macOS/Git Bash
```

没有 Blender 也能单独跑第二级（glb→dasset 是纯 C#，convert 失败会继续 cook）：

```bash
dotnet run --project Tools/FbxToGltf/Program/DassetCompiler.csproj -- --scan Tests/Game0/Resources/Models
```

DassetCompiler 也支持单文件模式：

```bash
dassetcompiler <input.glb|gltf> <output.dasset>   # 单文件
dassetcompiler --scan <dir>                        # 目录递归，增量
```

## 增量行为

- convert（`.fbx → .glb`）：`.glb` 不存在或比 `.fbx` 旧才重转。时间戳比较是
  "分"级精度（批处理 `%~t` 的固有限制）。
- cook（`.glb → .dasset`）：`.dasset` 不存在或比 `.glb` 旧才重转（UTC 时间戳比较）。
- 要强制重转，删掉对应产物文件即可。
- 单文件模式始终重新 cook。升级 cooker 后，旧 `.dasset` 若含 JPEG 贴图，需用单文件模式重烘焙；
  `--scan` 的时间戳检查不会自动发现编码策略更新。

## 依赖

- Blender 3.6+（仅第一级需要；自带 FBX 导入器与 glTF 导出器，无需额外插件）；
- 第二级是 `Program/` 下的 .NET 控制台项目（引用 DragonLib.Gltf + Engine）。

## 已知取舍

- 转换质量取决于 Blender 的 FBX 导入器（社区长期维护，覆盖二进制 FBX 7.x 的
  绝大多数导出；静态节点的变换烘焙进顶点；蒙皮/动画由 .dasset v2 消费：
  skin/剪辑进入骨架表与剪辑表，蒙皮 primitive 顶点保持 bind 空间）。
- PNG 原始字节保留，JPEG 使用 StbImageSharp 在离线阶段解码，再用 Foster 编码 PNG；
  保持像素、行序与 alpha，不额外应用 gamma 或颜色配置，颜色/数据用途仍由运行时区分。
  webp/dds/ktx2 在 cook 时跳过并警告；无需修改 Foster 的运行时解码器。
- 材质的 AlphaMode/AlphaCutoff 会写进 .dasset；渲染端 Opaque/Mask 走不透明队列
  （Mask 由 shader clip），Blend 走透明队列（back-to-front、不写深度）。

渲染侧约定（正面绕序/矩阵/法线变换/蒙皮 palette）的权威文档在
`Libs/Engine/Rendering/README.md`——cooker 改动涉及顶点/索引/法线/蒙皮前必读。
