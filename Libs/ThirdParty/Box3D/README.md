# DragonLib.Box3D

桌面 3D 物理接入模块，固定引用 [Miguel249/Box3D.NET](https://github.com/Miguel249/Box3D.NET) 的 NuGet **0.5.1**（托管绑定及 `Box3D.NET.Native` 均锁定精确版本范围 `[0.5.1]`，避免单独升级原生 ABI）。原生 Box3D 库由 Native 包携带，无须修改或拷贝上游源码。上游和原生引擎均为 MIT 许可，许可证及第三方声明随 NuGet 包提供。

本模块不依赖 Engine、Foster 或 ECS；需要物理的项目显式引用 `DragonLib.Box3D.csproj`。核心 Engine 不引入物理依赖。当前接入验证为 Windows x64；上游包提供其他桌面 RID，但本仓库尚未实测。**没有 WebAssembly 物理后端**，不要将本项目添加到浏览器项目。

## 世界与时间步

```csharp
using System.Numerics;
using Box3D;
using DragonLib.Box3D;

using var world = new Box3DWorld(new Vector3(0, -9.81f, 0));
var ground = world.Simulation.CreateStaticBody(new Vector3(0, -.5f, 0));
ground.AddBox(new Box(new Vector3(10, .5f, 10))); // Box 接收半尺寸
var body = world.Simulation.CreateDynamicBody(new Vector3(0, 4, 0));
body.UserData = 123; // 应用自己的实体/实例标识
body.AddBox(new Box(new Vector3(.5f)), ShapeDefinition.Default with
{
    EnableContactEvents = true,
});

// 每帧传入经过的秒数。默认 60 Hz、每步 4 个求解子步、最多补算 8 步。
world.Advance(frameDeltaSeconds, physics =>
{
    foreach (var contact in physics.Events.ContactBegins)
    {
        // 此处消费事件。不能保存事件缓冲区，下一次 Step 会覆盖它。
    }
});

// 仿真结束后，把位置/旋转写到自己的 Transform3DComp，再运行 Transform3DSystem。
var position = body.Position;
var rotation = body.Rotation;
```

3D 坐标采用 Y 向上，与 Camera3D/System.Numerics 一致；重力默认向 -Y。不沿用 2D demo 的 Y 向下约定，也不暗中翻轴或缩放单位。渲染矩阵使用 `Scale * Rotation * Translation`，碰撞体尺寸独立设置，变更 Transform 的 Scale 不会自动改变碰撞体。

`Step()` 恰好推进一个固定步，适合暂停时单步调试。`Advance()` 对超长帧限制补算，把未接受的时间累计到 `DroppedSeconds`，保留此前不足一整步的余量；不会在恢复后无限补帧。`InterpolationAlpha` 提供余量比例，应用如需渲染插值，应自行保存前后两步的变换。暂停/切场景可调用 `ResetAccumulator()`。

`Simulation` 暴露上游 API，可使用球、胶囊、关节、传感器、射线及其它能力，不重复包装全部 API。应用通过本模块的 Step/Advance 推进；直接调用 `Simulation.Step()` 会绕过步数统计。`WorldSettings.Default with { WorkerCount = ... }` 可选择原生工作线程；它不复用 Engine.JobScheduler。

## 生命周期与 ECS

世界只允许一个调用线程访问；不得在 Step 时读写同一世界。每个固定步的碰撞/移动事件须在 `Advance` 的回调中消费，不能只读一帧最后一个步的事件。

`Box3DWorld.Dispose()` 释放全部刚体、形状和关节。身体句柄不能跨世界重建保存：先清除应用的句柄映射，再销毁世界。主动删除实体时调用 `body.Destroy()`；`UserData` 应使用能识别实体代次的应用标识，不能把可能复用的裸实体索引视为永久身份。

此次提供世界/固定步接入和可运行示例；尚未提供通用 ECS 刚体组件、自动实体生命周期同步、父子层级物理绑定、物理资源序列化或角色控制器。网格/高度场等借用几何资源的释放顺序遵循上游：先销毁世界，再释放几何资源。

## 查看与验证

```powershell
dotnet run --project ../DragonLib.Tests/Game0/Game0.csproj
# 主菜单选择 Box3D Physics：箱子堆叠、斜坡、Drop box、暂停/单步、Reset、射线查询。

dotnet test ../DragonLib.Tests/Game0/Tests/Entities.Tests.csproj --filter FullyQualifiedName~Box3DIntegrationTests
dotnet run --project ../DragonLib.Tests/Box3D.Smoke/Box3D.Smoke.csproj
dotnet run --project ../DragonLib.Tests/Box3D.Smoke/Box3D.Smoke.csproj -- --vulkan
```

GPU smoke 与 Game0 共用 `Box3DScene`，检查初始/落地后的实际像素、碰撞事件、生成刚体、射线和重建世界；PNG 输出到 smoke 的构建目录。

2026-10-06 验证：新增 13 项 Box3D 测试通过，Engine/Entities 合计 143 项通过；测试解决方案构建和 Engine 的 `net10.0-browser` 构建通过。Windows x64 的 D3D12、Vulkan 实测均绘出完整初始/落地/重建场景，落地时捕获 12 次接触开始事件。浏览器构建通过只说明核心 Engine 仍可构建，不代表 Box3D 可在 Web 运行。
