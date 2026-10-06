# 独立动画能力（阶段 10）

所有 API 均在 Engine.Animation，不依赖 ECS/GPU。示例只在同级 DragonLib.Tests/Game0；库仓库不新增 Game0。原有 SkeletonAnimator.SamplePose、TRS 混合、128 joints palette 和 Renderer3D 蒙皮提交继续使用。

```csharp
var animation = new AnimationStateMachine3D(model.Skeletons[0]);
animation.AddState("walk", new AnimationState3D(walkClip, Markers: [new(.25f, "footstep")]));
animation.AddState("idle", new AnimationState3D(idleClip));
animation.AddTransition("idle", "walk", () => moving, fadeSeconds: .2f);
animation.Play("idle");
animation.RootMotionJoint = 0; // 必须是顶层关节；默认 -1，不提取运动

// 每帧：事件交给游戏，运动由调用者应用，没有物理/ECS 绑定。
animation.Update(deltaSeconds);
world = animation.DeltaRootMotion * world; // System.Numerics 行向量矩阵约定
foreach (var occurrence in animation.Events) HandleEvent(occurrence.Marker.Name);
CcdIk3D.Solve(animation.Skeleton, animation.Pose, endJoint, modelSpaceTarget);
animation.RebuildPalette(); // 手工改变 pose/IK 后需要重算
renderer.DrawModel(model, materials, world, animation.Palette);
```

## 播放与事件

AnimationPlayback3D.Position 是 double 绝对时间，Time 是采样时间；负 Speed 可倒放，deltaSeconds 必须非负。循环支持一次推进多圈；非循环在 [0, duration] 停住。Seek 跳转不会发事件或根运动，状态机 Seek 同时清除过渡。

事件按本次经过的时间排序：正放区间 (previous, next]，倒放 [next, previous)。暂停不发，停在非循环尾部不重复发。事件在 0 或 duration 时按照对应边界触发，播放/Seek 本身不会自动触发起点；两处同时配置不同事件时，它们会在循环边界分别触发。标记由应用注册，未更改 .dasset 格式。

单次最多输出 1024 个事件，丢弃数在 DroppedEvents 报告；异常巨大 dt 不应当用来重放长期离线游戏事件。

## 状态及过渡

注册条件过渡或显式 Play(name, fadeSeconds)，每次 Update 最多选一个条件过渡，按注册顺序决定优先级。普通过渡期间源/目标剪辑同时推进，按 TRS 混合；过渡被打断时从当前混合姿态开始淡入，不跳回上一条剪辑。只暴露目标剪辑事件，源事件不重复交付。源状态可以继续播放，暂停/速度由 PlaybackSpeed 控制当前状态；条件与事件业务由应用处理。

不是大型 Animator 图编辑器：目前没有参数图、blend tree、逐骨骼遮罩或同步组。

## 根运动

RootMotion3D.Extract 可以单独使用。提取顶层关节的 translation/rotation 刚体增量，忽略根 scale；循环首尾的运动通过矩阵幂连续累计，支持多圈与倒放，不将 wrap 错当成回跳。返回 delta 应左乘世界矩阵。RemoveFromPose 恢复根 bind translation/rotation，避免 palette 与世界矩阵重复移动。

状态机开启 RootMotionJoint 后自动移除 pose 中对应运动，DeltaRootMotion 只来自目标剪辑；过渡不混合两条运动轨迹。Seek/Play 不产生移动。多根骨架由调用者选一个；这里只处理完整平移及旋转，未实现轴向筛选、地面约束或物理角色控制。

## 基础 IK

CcdIk3D.Solve 接收模型空间目标和末端关节，修改最多 chainLength 个祖先的本地 rotation，返回最终末端误差；translation/scale 保持不变。可配置迭代、容差、weight。要求关节正的均匀 scale；非均匀 scale 显式拒绝。支持完全反向直链的初始退化处理。

CCD 是迭代方法，不保证每个目标全局收敛；无法到达的目标或局部极小值会保留有限迭代后的姿态，通过返回误差判断。尚无 joint limits、pole vector、脚底锁定或全身 IK。世界目标先用 inverse(world) 转到模型空间。

## 查看与验证

运行 DragonLib.Tests/Game0，选择 **Skinning (animation / IK)**。三个实例由独立状态机驱动：Blend wave/rest 看过渡，CCD IK 显示绿色目标，Root motion (stride) 看位移，Reset positions 恢复位置；面板显示动画事件计数。现有 ECS 外壳只负责示例生命周期，实例动画/变换不创建 ECS 组件。

Animation3D.Smoke 一次定向 CPU 运行覆盖跨圈及倒放事件、停止边界、TRS 过渡/打断、跨圈根平移/旋转和 CCD 可达/反向目标。未新建大量单测或重复运行渲染回归；桌面示例和 Engine browser 构建通过。
