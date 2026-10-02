# 程序化肠壁 Demo

启动 Game0，在 `Scene Tests` 中选择 **Procedural Gut Wall**。也可以从仓库根目录直接运行：

```powershell
dotnet run --project Tests/Game0/Game0.csproj -- --gut-demo
```

默认生成一块带弯曲外轮廓的连续肠壁，内部充满细长褶皱、共享沟槽和细纹。鼠标移动青绿色点光源；右侧还有可关闭的暖色补光。

## 建议的观察顺序

1. 在 `View` 中依次查看 **Height**、**Normals**、**Albedo** 和 **Lit surface**。
2. 移动鼠标，观察同一条褶皱的亮边随光源改变。
3. 将 `Normal strength` 调到 0，比较平面法线与褶皱法线的差别。
4. 将 `Wet highlights` 调到 0，比较漫反射和湿润高光。
5. 关闭 `Follow mouse light`，手动调整 `Light XY`；或启用 `Orbit light` 自动巡游。
6. 调整种子、尺度 `Fold scale`、沟槽宽度 `Groove width`、细纹 `Fine striations` 和弯曲程度，点击 `Regenerate`。相同参数可复现相同纹理。

整个轮廓内部都有组织高度；深色区域表示凹陷沟槽，不是短条之间露出的平底。`Fold scale` 改变纹理尺度，`Groove width` 改变沟槽截面，`Fine striations` 控制顺着大褶皱的细纹。相邻褶皱共用边界，不再用独立曲线的宽度或避让距离决定是否填满。

## 实现

`GutFoldTexture.cs` 使用 Gray-Scott 反应扩散模拟，从带种子的局部活跃区域长出连续的弯曲带状纹路。空间变化的扩散方向和反应参数控制局部并行、分支与回绕，轻微坐标扭曲增加宽度变化。将生长结果映射为圆润的连续高度剖面，并沿该剖面叠加肩部色带、细纹和组织颜色变化，最后合成外轮廓与凸起边缘。高度梯度产生法线；同一高度场提供颜色和材质参数。此算法是对参考图视觉结构的程序化近似，不代表原游戏的实际制作方法。

`GutWallDemo.cs` 只在初次进入或重新生成时烘焙三张 1400×800 纹理。模拟在后台运行，窗口显示 `Growing folds...`；GPU 上传在游戏线程完成。默认参数在本机 Debug 运行中烘焙约 4.4 秒，生成期间窗口保持响应；重新生成时保留旧纹理，完成后替换。程序关闭时取消未完成的模拟。运行时绘制一个矩形，不创建每条褶皱的实体。离开 Demo 时恢复相机。

`GutWall.hlsl` 读取纹理，在线性颜色空间计算点光源漫反射和高光。法线的 X/Y 方向对应世界坐标，Y 向下，Z 朝向观察者。灯光高度影响入射方向；褶皱高度用于微调表面 Z。此示例演示法线光照，没有褶皱投影阴影或真实的几何位移。

**Export maps (PNG)** 将文件保存至可执行文件旁的 `GutWallExport`：

| 文件 | 内容 |
| --- | --- |
| `gut-albedo.png` | sRGB 颜色；A 是轮廓遮罩 |
| `gut-normal.png` | RGB = 法线 × 0.5 + 0.5；作为线性数据读取 |
| `gut-material.png` | R = 高度 / 0.6，G = 粗糙度，B = 凹槽环境遮蔽，A = 轮廓遮罩；作为线性数据读取 |

这些贴图可以独立使用；图形管线使用 Y 向上的法线约定时，需要翻转法线绿色通道。

## Shader 编译与渲染验证

仓库中的 `Tests/Game0/Shaders/compile.bat` / `compile.sh` 会自动编译 `GutWall.hlsl`；已包含 SPIR-V、MSL、DXIL 产物。

设置 `GUT_DEMO_CAPTURE` 为输出目录后，程序自动进入此 Demo，导出原始贴图和 GPU 渲染的颜色、高度、法线、两侧灯光、无凹凸和无高光对照，然后退出。示例：

```powershell
$env:GUT_DEMO_CAPTURE = Join-Path (Get-Location) '.codex-build/gut-wall'
try {
    dotnet run --project Tests/Game0/Game0.csproj -- --gut-demo --no-focus
} finally {
    Remove-Item Env:GUT_DEMO_CAPTURE
}
```
