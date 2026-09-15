# FbxToGltf — FBX 离线转换工具

DragonLib 运行时**只加载 glTF**（.glb/.gltf，见 `Libs/ThirdParty/Gltf`）。FBX 是
Autodesk 私有格式、没有官方 .NET SDK，因此按业界惯例在**导入期**转成 glTF，
产物 .glb 放在 FBX 同目录，`GltfModelScanner` 启动时会自动注册（资产名 = 相对
`Resources` 的路径去扩展名，如 `Models/character`）。

## 依赖

- Blender 3.6+（自带 FBX 导入器与 glTF 导出器，无需额外插件）。

## 用法

```bash
# 任选一种方式让脚本找到 Blender：
#   1) Blender 在 PATH 里
#   2) 设置环境变量 BLENDER 指向 blender.exe
#   3) 直接传路径（Windows 会额外探测 Program Files\Blender Foundation\*\blender.exe）

Tools/FbxToGltf/convert.bat                        # Windows
Tools/FbxToGltf/convert.bat "C:\...\blender.exe"   # Windows，显式路径
Tools/FbxToGltf/convert.sh                         # Linux/macOS/Git Bash
```

脚本遍历 `Tests/Game0/Resources/Models/**\*.fbx`：

- `.glb` 不存在、或比 `.fbx` 旧 → 重新转换（Blender headless，贴图内嵌、带切线）；
- 否则跳过。时间戳比较是"分"级精度（批处理 `%~t` 的固有限制）。

## 工作流建议

放入新 FBX 后跑一次脚本即可；也可以挂到构建前事件，或每次手动跑——增量判断
保证没有变化的文件不会重转。要强制重转，删掉对应的 .glb。

## 已知取舍

- 转换质量取决于 Blender 的 FBX 导入器（社区长期维护，覆盖二进制 FBX 7.x 的
  绝大多数导出；动画/蒙皮会随 glTF 导出，但 DragonLib 加载端本期只消费静态网格）。
- 若需要运行时直读更多格式（OBJ/DAE 等），后续可评估 AssimpNet（原生 assimp.dll），
  当前刻意不引入运行时原生依赖。
