# Engine 与可选 ECS 扩展

不使用 ECS 的游戏只需引用 `Libs/Engine/Engine.csproj`。Engine 提供渲染、音频、资源、相机、动画计算、线程调度和通用工具，不引用 DragonECS。

使用 ECS 的游戏额外引用 `Libs/Engine.ECS/Engine.ECS.csproj`。该项目依赖 Engine、DragonECS 和 DragonECS-AutoInjections，提供组件、System、Pipeline 扩展、实体检查器，以及 ECS 预制体和关卡序列化。

```text
普通游戏 → Engine
ECS 游戏 → Engine.ECS → Engine
                     → DragonECS / DragonECS-AutoInjections
```

## 通用功能

- `Engine.World.SceneRouter<TScreen>`：场景切换及过渡进度，由游戏调用 `Update(dt)`。
- `Engine.Messaging.CommandQueue<TCommand>`：单消费者命令队列，由消费者调用 `Drain`。
- `Engine.Messaging.BroadcastChannel<TMessage>`：下一帧可见的广播消息；普通游戏在每帧开始、生产者和消费者运行之前调用一次 `AdvanceFrame()`。
- `Engine.Animation.SkeletonAnimator`：独立的骨骼动画计算；ECS 扩展中的 `AnimationSystem` 负责读取组件并调用它。

ECS 游戏通过 `using Engine.ECS;` 使用 `AddCommandQueue` 和 `AddBroadcastChannel` 接入 Pipeline；`AddBroadcastChannel` 会自动在每次 Update 开始时推进消息，不需要游戏再手动调用 `AdvanceFrame()`。

## 从旧版迁移

1. 使用 ECS 的项目添加 `Engine.ECS.csproj` 引用。原有 ECS 组件、System 和序列化类型继续使用 `Engine.ECS` 命名空间，预制体和关卡 JSON 格式保持不变。
2. 使用 `SceneRouter` 的文件改为导入 `Engine.World`。
3. 使用 `CommandQueue` 或 `BroadcastChannel` 的文件导入 `Engine.Messaging`；调用 Pipeline 扩展时同时导入 `Engine.ECS`。

新增通用能力时，先让它能在 Engine 中独立调用，再在 Engine.ECS 中提供读取组件、注入依赖和驱动更新的接入代码。

## 验证

```powershell
dotnet test Tests/Engine.Tests/Engine.Tests.csproj
dotnet test Tests/Game0/Tests/Entities.Tests.csproj
dotnet build Tests/Game0/Game0.sln
```

`Engine.Tests` 只引用 Engine，验证通用消息行为，并检查应用的依赖清单中没有 Engine.ECS 或 DragonECS。Game0 测试覆盖 ECS 接入及现有游戏功能。
