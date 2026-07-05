# Assets 资源库

一套为编辑器与运行时共用的轻量资源系统。核心思想借鉴 Celeste 作者 Noel 与 Godot：
**资源的"名字"（相对路径）才是唯一真相，id 只是名字的确定性哈希投影。**
因此不需要中央 id 表 / sidecar：运行期扫描目录时凭路径重建映射即可。

代价：**重命名 / 移动资源会改变 id，旧引用会失效**（这是刻意接受的取舍）。

---

## 三种 id，先分清

| 类型 | 指向 | 怎么来 | 作用域 | 存哪 |
|------|------|--------|--------|------|
| `AssetId` | 一份**内容**（sprite / tileset / prefab） | 路径名的 FNV-1a 64bit 哈希 | 全局、永久 | 运行期从目录重建 |
| `SpawnId` | 关卡里**被摆放的一个实例** | Snowflake 发号（唯一） | 单个关卡内 | 存进关卡文件 |
| `EntityRef` | 指向"同一关卡里另一个实例" | 包一个 `SpawnId` | 单个关卡内 | 存进关卡文件 |

关键区别：`AssetId` 指向"内容"，同名永远同 id、可从名字重算；
`SpawnId` 指向"这一个"，同一预制体摆 10 次要 10 个不同 id，所以必须发号、不能靠名字算。

---

## 核心概念：同名字 → 同 id

`AssetId.FromName` 是纯函数：相同字符串永远算出相同 id，跨机器、跨重启一致。

```csharp
AssetId.FromName("objects/lift_outskirts");   // 永远是同一个 long
```

因此"关掉编辑器再打开"没问题：重启后重新扫描目录，同一个文件名算出同一个 id，
和存档里的哈希自动对上。**id 不需要被记住，因为它随时能从名字重算。**

正因为"同字符串同 id"，字符串只要差一点 id 就完全不同。所以哈希前会先归一化：

```csharp
AssetId.Normalize("Objects\\Lift.ase")  // → "objects/lift"（正斜杠、去扩展名、小写）
```

`"objects/lift"`、`"Objects/Lift.ase"`、`"objects\lift"` 归一化后都是同一个 id。

> **注意**：id → name 无法反算（哈希单向）。编辑器能显示名字，是因为扫描时
> 顺手建了一张反向表。若资源文件已被删除 / 改名，这次扫描登记不到它，
> 反查会失败，编辑器只能显示 `<missing 十六进制>`。这是哈希方案的固有代价。

---

## 用法

### 1. 建库并登记资源（扫描 / 加载阶段）

`AssetDatabase` 不关心资源从哪来，你把加载好的对象按"相对路径名"登记进去即可。

```csharp
var assets = new AssetDatabase();

// name 用相对路径风格，扩展名可有可无（会被归一化掉）
assets.Register("objects/lift_outskirts", liftSprite);
assets.Register("objects/door",           doorSprite);
```

真实项目里这一步是个遍历目录的循环（"资源扫描器"，尚未实现，依赖你的
`.ase → Sprite` 加载方式）。规则始终是：**名字 = 相对资源根目录的路径、去扩展名。**

### 2. 组件里用 `AssetRef<T>` 引用资源

```csharp
public struct ElevatorComp : IEcsComponent
{
    public AssetRef<Sprite> Spr;   // 序列化成裸 long（哈希 id）
    public bool Locked;
    public Vector2Int Destination;
}
```

### 3. 运行时解析回真对象

```csharp
Sprite? spr = assets.Get(elevator.Spr);   // AssetRef<Sprite> → Sprite?
if (spr != null) { /* 渲染 */ }

// 也可直接按 id 或名字取
Sprite? a = assets.Get<Sprite>(AssetId.FromName("objects/door"));
Sprite? b = assets.Get<Sprite>("objects/door");
```

### 4. 序列化

`AssetRef<T>` / `SpawnId` / `EntityRef` 都有内置 `JsonConverter`，直接写成裸 long：

```csharp
var opt = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
string json = JsonSerializer.Serialize(elevator, opt);
// {"Spr":15634960586538533060,"Locked":true,"Destination":{"X":0,"Y":192}}
```

读回来后 `assets.Get(...)` 再解析。磁盘上就是 Noel 那种 `"spr": 15634...` 的形状。

### 5. 编辑器下拉（截图里的 `Spr` 行）

`AssetRefDrawer<T>` 接进现有的 `PropertyDrawer` 系统。启动时绑定数据库并注册：

```csharp
AssetRefDrawer<Sprite>.Bind(assets);                          // 告诉 drawer 用哪个库查名字
PropertyDrawerRegistry.Register(new AssetRefDrawer<Sprite>()); // 让 AssetRef<Sprite> 字段可编辑
```

之后 inspector 里任何 `AssetRef<Sprite>` 字段都会渲染成下拉：列出所有已登记的
sprite 名字，选中存哈希，`(none)` 清空，引用失效时显示 `<missing hex>`。

放进 `EcsInspectorSystem` 的 `registerDrawers` 回调最顺：

```csharp
.Add(new EcsInspectorSystem(() => {
    AssetRefDrawer<Sprite>.Bind(assets);
    PropertyDrawerRegistry.Register(new AssetRefDrawer<Sprite>());
}))
```

---

## SpawnId：关卡里"这一个"的身份, 关卡中的唯一Id

`AssetId` 回答"长什么样"（用哪个 sprite），`SpawnId` 回答"是哪一个"。
同一个电梯预制体在关卡里摆 3 台，它们 `AssetId` 相同、`SpawnId` 各不相同。
`SpawnId` 的唯一用途是**让关卡里一个对象引用另一个对象**（门开哪个开关）。

它本身 API 很小：`New()` / `Value` / `IsValid` / `ToHex()`。真正的"用法"是下面这套
**发号 → 组件承载 → EntityRef 交叉引用 → 两趟加载回填**的模式。

### 1. 摆放时发号

```csharp
var id = SpawnId.New();   // Snowflake 发号，全局唯一、天然有序
```

### 2. 用组件让实体带上自己的身份

```csharp
public struct SpawnIdComp : IEcsComponent { public SpawnId Id; }

int e = world.NewEntity();
world.GetPool<SpawnIdComp>().Add(e).Id = SpawnId.New();
```

### 3. 交叉引用用 `EntityRef`，不要存 entlong

运行时的 `entlong` 不能序列化；要引用另一个实例，存它的 `SpawnId`（包在 `EntityRef` 里）：

```csharp
public struct DoorComp : IEcsComponent
{
    public EntityRef OpenedBy;   // 指向那个开关实例，序列化成裸 long
}

door.OpenedBy = new EntityRef(switchSpawnId);
```

序列化后：

```json
{ "Id": 5855663881021371804 }        // 开关自己的身份
{ "OpenedBy": 5855663881021371804 }  // 门指向那个开关
```

### 4. 加载必须两趟（关键）

`SpawnId` 存的是身份数字，运行时要靠它找到真正的实体。门可能在开关之前加载，
所以**先建完所有实体、建好 `SpawnId → entlong` 映射，再回填交叉引用**：

```csharp
// 第 1 趟：建所有实体 + 记映射
var map = new Dictionary<SpawnId, entlong>();
foreach (var data in level.Entities)
{
    int e = world.NewEntity();
    world.GetPool<SpawnIdComp>().Add(e).Id = data.Id;
    // ... 加其它组件（含带 EntityRef 的组件，此刻 Target 只是个 SpawnId，先不解析）
    map[data.Id] = world.GetEntityLong(e);
}

// 第 2 趟：所有实体都在了，才能把 EntityRef 解析成真实体
foreach (int e in world.Entities)
{
    ref var door = ref world.GetPool<DoorComp>().Get(e);   // 举例
    if (door.OpenedBy.IsValid && map.TryGetValue(door.OpenedBy.Target, out var target))
    {
        // target 是 entlong；运行时可缓存它，或每次用时查 map
    }
}
```

> **作用域**：`SpawnId` 只在**单个关卡内**唯一且有意义。跨关卡引用（A 关卡的门开
> B 关卡的开关）不在此设计内——那需要"关卡 id + SpawnId"的复合引用，独立游戏一般
> 用不上，别提前设计。

---

## 文件一览

| 文件 | 内容 |
|------|------|
| `AssetsId.cs` | `AssetId` + `FromName` + FNV-1a + `Normalize` + `ToHex` |
| `AssetIdGenerator.cs` | Snowflake 发号器（服务 `SpawnId`；WorkerId：游戏 0 / 编辑器 1 / 工具 2） |
| `Handles.cs` | `SpawnId`、`AssetRef<T>`、`EntityRef` 及各自的 JsonConverter |
| `AssetRegistry.cs` | 运行期 `AssetId → object` 映射（纯存取） |
| `AssetDatabase.cs` | 门面：登记 / 类型化解析 / 反向名字表 / 哈希碰撞检测 |
| `AssetRefDrawer.cs` | `AssetRef<T>` 的 ImGui 下拉绘制器 |

---

## 尚未实现（后续）

- **资源扫描器**：遍历 `Content/Sprites/**`，`.ase → Sprite`，按相对路径 `Register`。
- **关卡序列化** `SaveLevel / LoadLevel`：ECS 世界 ↔ `{def, id, fields}` 关卡 JSON，
  含 `SpawnId → entlong` 两趟引用回填。

---

## 设计约束备忘

- **改名 = 断链**：可接受，换来零 sidecar / 零中央表。要排查断链，只能靠资源还在时的反向表。
- **碰撞即崩**：`AssetDatabase.Register` 发现两个不同名字撞同一哈希会抛异常，绝不静默覆盖。
- **哈希不缓存**：`FromName` 每次现算，几千资源无所谓；成热点再优化。

