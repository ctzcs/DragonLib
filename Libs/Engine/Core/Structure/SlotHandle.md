# SlotHandle 约定

通过索引 + 代数（`Idx`、`Gen`）定位元素，识别已删除、已复用的槽位。约定对齐 olib/handle。

`SlotHandle` 是槽位句柄的通用原语；领域专用的句柄（如 `EntityHandle`、`JobHandle`）
各自定义类型，不要混用。Assets 里的 `SpawnId`/`AssetId`/`AssetRef` 是可序列化的
内容标识，与运行时槽位句柄是不同概念。

## 核心规则

- `SlotHandle` = `Idx + Gen`。idx 0 永远是 dummy 槽，零句柄（`SlotHandle.None` / `default`）始终无效。
- 槽位复用时 `Gen` +1，旧句柄自动失效；`Gen` 为 32 位，回绕后不保证历史句柄仍然无效。
- 句柄只属于创建它的容器实例；相同元素类型的两个容器之间也不能互用。
- `Item<T>.Handle` 由容器维护，调用方不应修改。

## 选型（对应 olib/handle 的子包）

| 类型 | 对应 olib | 适用场景 |
| --- | --- | --- |
| `SlotMap<T>` | `handle/array` | 默认选择；满时倍增扩容，句柄始终有效，但扩容会使旧 ref/Span 失效 |
| `FixedSlotMap<T>` | `handle/fixed` | 已知硬上限的池（粒子、弹幕）；满后 `TryAdd` 返回 false、`Add` 抛异常 |

olib 另有两个变体没有映射：`growing`（元素地址稳定）解决的是"长期持有元素指针"，
C# 的 ref 本来就存不进字段，存 `SlotHandle` 再解析即可；`virtual`（预留虚拟内存原地扩容）
要求把元素放进非托管保留页，托管 `T` 放不进去。等有 `unmanaged` 数据的真实需求
（如互操作缓冲）再考虑加 `ChunkedSlotMap` 之类的变体。

两个类型共享同一套核心实现（`FixedSlotMap` 继承 `SlotMap` 并拒绝扩容），
公共操作完全一致：`Add/TryAdd/Get/TryGet/IsValid/Remove/Clear/Reset/Count/Capacity` + foreach。

## 生命周期

| 操作 | 内容和内存 | 句柄语义 |
| --- | --- | --- |
| `Remove(h)` / `Remove(h, destroy)` | 移除一个元素，槽位可复用 | 该元素旧句柄立即失效；无效句柄是 no-op |
| `Clear(destroy)` | 移除全部活跃元素，保留槽位、代数与容量 | 旧句柄继续失效，直到代数回绕 |
| `Reset(destroy)` | 清零槽位历史，保留容量 | 开始新生命周期，调用方必须丢弃全部旧句柄 |

三个操作都可传 destroy 回调对活跃元素逐项清理（对应 olib 的 `destroy: proc(value: ^T)`）。
回调不得增删、清空或重置同一个容器；容器不自动释放 `T` 内部持有的资源。
可扩容实现把分配集中在 Add（扩容时一并预留空闲链容量），Remove/Clear 永不分配。

## 用法

```csharp
var pool = new SlotMap<Entity>(1024);        // 或 FixedSlotMap<Entity>(1024) 定容

SlotHandle h = pool.Add(entity);             // FixedSlotMap 满时抛 InvalidOperationException
if (pool.TryAdd(entity, out var h2)) { ... } // 满时安静失败

if (pool.IsValid(h))
{
    ref Item<Entity> item = ref pool.Get(h); // 无效句柄返回 default 落点，不抛
    item.Value.Pos += delta;
}

pool.Remove(h);                              // 旧句柄失效
foreach (var item in pool) { ... }           // 跳过空槽；item 是副本，改值需手动循环取 ref
```

- `IsValid` 是完整校验（idx 范围 + 槽位句柄一致），做防御用；热路径可直接 `Get` 后判 `item.Handle.IsNone`。
- `Get` / 索引器对无效句柄返回 default 落点（`Handle == None`），永不抛异常、永不返回别的元素。
- 容器非线程安全；迭代期间不要结构性修改。
