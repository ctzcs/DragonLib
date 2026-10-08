# DragonLib 面向数据开发约定

本文件是用 DragonLib 编写游戏时的面向数据设计（Data-Oriented Design，DOD）约定，适用于新项目和已有项目的日常开发。工程目录、UI、发布脚本和创建流程见 [新项目创建提示词](New-project.md)；本文件补充“数据怎么放、规则怎么写、缓存和配置怎么管、怎样证明行为没变”。

使用方式：让 AI 阅读本文件后再改代码，例如“阅读 DragonLib 的 Prompts/DOD-guidelines.md，按其中约定为 MyGame 加一种敌人”。也可以复制“提示词开始”到“提示词结束”之间的内容放进游戏仓库的 `AGENTS.md`。下文的 `ActiveWorld`、`Derived`、`Loc` 等是推荐的类型名，不是 DragonLib 已提供的 API；在游戏中实现时可以改名，但职责保持一致。

## 提示词开始

你在一个以面向数据设计为核心的 C# 游戏项目中工作，引擎为 DragonLib。目标是：模拟可确定、可在无窗口时测试；热点循环不分配；内容由数据驱动；各层边界清楚。写代码前先判断它属于哪一层、读写哪些数据列；规则不确定时先读项目的 `Scripts/Core/Content/README.md` 和相关源码。

### 1. 实体：SlotMap + SoA 属性列

- 大量同类实体放在 `Engine.SlotMap<T>` 中，`T` 只保存所有实体共有的少量字段（种类、位置等）。跨帧引用一律保存 `SlotHandle`（槽位 + 代数），使用前用 `IsValid` 校验；实体删除后旧句柄自动失效，不用对象引用指向实体。
- 各类实体的专有属性按结构数组（SoA）存放，例如 `float[] Hp`、`byte[] Type`、`SlotHandle[] Target`，下标与槽位对齐。新增字段即新增一列，并同步检查分配、初始化、扩容、删除、槽位复用和重开，不能把旧值留给复用该槽位的新实体。
- 不为每个实体创建带 `Update()` 的对象、组件类或虚方法。行为是对若干列做批量循环的函数。
- 实例只存 1 字节类型编号和自身会变化的数据；价格、射程等类型数值去目录里查（见第 3 节）。
- 遍历原始槽位（`Data` 到 `Touched`）时用 `Item<T>.IsLive` 跳过空槽。扩容后旧的 `ref`、`Span` 和枚举器失效，需要重新解析句柄。热点循环不用 LINQ、不装箱、不创建临时集合。

### 2. 分层与依赖方向

命名空间与目录一致；先按职责分层，再在层内按玩法分模块。

| 层 | 回答的问题 | 可以引用 |
| --- | --- | --- |
| Authoring | 有哪些配置、默认值是多少、文件如何读写和迁移（纯函数） | 不引用游戏层与图形设备 |
| Definitions | 有哪些类型、固定映射是什么 | Authoring |
| Runtime | 这一局现在是什么状态：SlotMap、SoA 列、对局状态、位图、版本号 | Definitions、Authoring、Engine 基础存储 |
| Logic | 给定状态、命令和 `dt`，世界如何变化；可重建的查询缓存 | 以上各层；不引用宿主、输入、窗口、Paper、渲染、音频 |
| Integration | 输入、时钟、生命周期如何接到 Logic；系统执行顺序 | 全部 |
| Presentation | 状态如何显示和播放；显示缓存 | 全部，但不推进模拟、不另存权威状态 |

- Runtime 只维护存储一致性（分配、删除、扩容、版本号），不决定“是否合法”“收益多少”。
- 规则写在 Logic，签名显式接收状态和时间，例如 `Step(sim, state, dt)`；在无窗口检查中可以直接调用。
- 不用转发类、全局 using 或兼容壳掩盖跨层引用。层界用架构检查程序按真实符号（含别名与全限定名）验证，不靠字符串搜索。

### 3. 数据驱动的内容目录

- 建筑、单位、敌人、首领、兴趣点、地形等定义为“目录”：有序、以字符串 ID 作 JSON 键，第一项为默认项，运行时以其下标作为 1 字节类型编号。关卡只覆盖个别条目的个别字段。
- 条目行为由**能力字段**组合（攻击方式、挡路、照明、产出、床位、可升级……）。代码按能力查询，**不按 ID 分支**；ID 常量只给测试和工具用，并由架构检查禁止游戏代码引用。
- 热点循环使用从目录预展开的规则数组，如 `rules.Attacks(type)`、`rules.Walkable(kind)`。规则对象按目录引用缓存，目录被替换后自动重建（见第 4 节的 `Derived`）。
- 默认值写在 C# 配置类型中，并用项目自定义的特性描述字段，例如 `[Field(名称, 说明, Group=…)]`、`[Choice(名称)]`，范围用 `[Range]`。编辑器表单、JSON Schema 和校验都从这些元数据生成，不手写第二份；Schema 由工具生成，检查程序比对是否过期。
- 优先级固定为：代码默认值 → 全局 JSON → 关卡稀疏覆盖。载入时完整校验，失败则保留上一份有效配置；旧字段在载入时迁移，旧文件继续可读。

### 4. 全局状态只有一处

- 所有可变的全局状态（世界模式、地图尺寸、地标、夜晚计划、当前生效的数值配置等）集中在一个静态类，推荐命名为 `ActiveWorld`，只能经它的方法修改。它提供整体快照 `Capture()` / `Restore()`，用于启动失败回滚和检查程序复原。
- 不写转发属性（`public static int Cost => Config.Current.Economy.Cost`）。调用方直接读 `ActiveWorld.Tuning.Economy.Cost`，读写入口只有一个。
- 派生数据（布局、场景、规则数组、名称表、菜单）通过一个小工具 `Derived<TSource, T>` 缓存：按**来源对象的引用**判断是否变化，来源被替换就重建。不再手写需要人工失效的静态缓存。来源必须是不可变快照，修改时整体替换，不能原地修改。
- 玩家设置（例如界面语言）可以另设一处全局状态，但不得影响模拟结果；与文本有关的缓存把语言版本也作为来源。

### 5. 变化事件与增量缓存

- 会被缓存观察的数据经统一入口修改（例如 `SetBuildingPosition`，或包装列写入的 `ChangeColumn<T>`），只在值真正变化时发布变化事件并递增版本号。
- 查询索引（空间桶、索敌树、工作分配索引）属于 Logic，按模拟实例持有，不挂回 Runtime。它们订阅变化事件，记录脏槽位，在下次查询前批量处理。回调只能记录，不能重入修改模拟。
- Presentation 缓存（实例化缓冲、地形块、反馈特效）只订阅、不驱动；切换模拟、重开或退出时解除订阅并释放 GPU 资源。
- 每个缓存在注释或文档中写明：输入、失效条件、重建时机、所有者与释放责任。缓存只加速查询，规则仍由对应 Logic 决定；可以提供关闭缓存的对照模式，用检查程序证明结果一致。

### 6. 确定性与并行

- 模拟以固定步长推进（默认 60 Hz）。暂停和倍速由 Integration 决定一帧执行几个固定步；Logic 不读真实时间、按钮或倍率。
- 需要可重放时，随机数使用固定种子，处理和提交都按槽位顺序进行，不依赖字典或哈希集合的遍历顺序。
- 并行只用于只读阶段（如批量索敌）：工作线程读取稳定数据，只写自己的结果区间；SlotMap、导航、视野和 GPU 只由主线程修改，并按原槽位顺序提交结果。
- 表现动画不保存插值中间值，只记录事件发生时刻，绘制时用缓动曲线算出姿态。

### 7. 文本与显示

- 玩家可见文字一律经本地化表读取，例如 `Loc.Get("hud.restart")`、`Loc.Get("build.needGold", cost)`，用占位符写整句，不拼接句子片段。内容名称按目录 ID 翻译（如 `Building.Catapult.Name`），缺少翻译时回退为配置里的原名。
- Logic 可以返回拒绝原因或通知文本，但文本只用于显示，不参与模拟判断。

### 8. 验证

- 每个玩法模块配一个无头检查程序（`dotnet run --project tests/<Feature>Checks`），直接调用 Logic 并断言状态；画面与输入另用冒烟程序注入真实点击并截图查看。
- 检查中的期望值从配置公式计算（例如 `Wild.NightBaseEnemies + Wild.NightGrowthPerDay`），不写死数字，以免调参导致误报。
- 结构重构和性能优化要证明行为不变：用固定场景的 Release 压测，对比关键结果（如开火数、击杀数、被毁建筑数）完全一致，再比较耗时。
- 调整层界或职责后运行架构检查，并更新对应目录 README 中的职责表和“修改入口”表。

### 9. 改代码时的默认步骤

1. 判断改动属于数据（新列、新目录字段）、规则（Logic）、接线（Integration）还是显示（Presentation）。
2. 新行为优先做成目录能力字段 + 按能力查询，而不是新类或 ID 分支。
3. 新状态只放入 Runtime 的列或对局状态；新的全局量只能进入集中状态类，派生量用 `Derived`。
4. 新缓存写明来源、失效条件和所有者。
5. 热点路径不引入逐实体对象、虚方法更新、反射或分配。
6. 改完运行相关检查和冒烟；修改默认数值时，同时更新代码默认值、JSON 和生成的 Schema。

## 提示词结束

## 示例

以下示例只说明写法，名称按项目替换。

**SoA 属性列与槽位对齐**

```csharp
namespace Core.Content.Runtime.Entities;

/// <summary>单位的专有属性列，下标与实体 SlotMap 的槽位一致。</summary>
public sealed class UnitData
{
    public byte[] Type = new byte[256];          // 目录中的类型编号
    public float[] Hp = new float[256];
    public SlotHandle[] Target = new SlotHandle[256]; // 无目标时为 SlotHandle.None

    /// <summary>槽位分配或复用时调用，清掉上一位占用者留下的值。</summary>
    public void Reset(int slot, byte type, float maxHp)
    {
        EnsureCapacity(slot + 1);
        Type[slot] = type; Hp[slot] = maxHp; Target[slot] = SlotHandle.None;
    }

    private void EnsureCapacity(int size)
    {
        if (size <= Hp.Length) return;
        int next = Math.Max(size, Hp.Length * 2);
        Array.Resize(ref Type, next); Array.Resize(ref Hp, next); Array.Resize(ref Target, next);
    }
}
```

**按能力查询的规则数组**

```csharp
/// <summary>从建筑目录展开的能力数组，供热点循环按类型编号查询。</summary>
public sealed class BuildingRules
{
    private readonly bool[] _attacks = new bool[256];
    private readonly int[] _sight = new int[256];

    internal BuildingRules(BuildingCatalog catalog)
    {
        for (int i = 0; i < catalog.Count; i++)
        {
            var building = catalog.Get(i);
            _attacks[i] = building.Attack != AttackKind.None;
            _sight[i] = building.SightRadius;
        }
    }

    public bool Attacks(byte type) => _attacks[type];
    public int Sight(byte type) => _sight[type];
}
```

**唯一的全局状态与按来源重建的派生视图**

```csharp
/// <summary>按来源引用缓存的派生值；来源被替换时重建。</summary>
public sealed class Derived<TSource, T>(Func<TSource> source, Func<TSource, T> build) where TSource : class
{
    private TSource? _from;
    private T? _value;

    public T Value
    {
        get
        {
            var current = source();
            if (!ReferenceEquals(current, _from)) { _value = build(current); _from = current; }
            return _value!;
        }
    }
}

/// <summary>进程内唯一的可变全局状态：当前世界与生效数值。</summary>
public static class ActiveWorld
{
    public static TuningDefinition Tuning { get; private set; } = new();

    private static readonly Derived<BuildingCatalog, BuildingRules> RulesView =
        new(() => Tuning.Buildings, catalog => new(catalog));

    /// <summary>建筑能力数组；数值重载后自动按新目录重建。</summary>
    public static BuildingRules BuildingRules => RulesView.Value;

    /// <summary>载入并校验配置；完整候选有效后才替换，失败时保留旧值。</summary>
    public static void LoadTuning(TuningDefinition validated) => Tuning = validated;
}
```

## 反面例子

| 不要这样 | 改为 |
| --- | --- |
| `class Tower { void Update() { … } }`，每座塔一个对象 | 塔的属性放在 SoA 列里，`TowerLogic.Step(sim, dt)` 批量处理 |
| `if (building.Id == "Catapult") Splash(…)` | 目录字段 `Attack = Splash`，按能力分支 |
| `public static int Cost => Config.Current.Cost;` 散在多个类里 | 直接读 `ActiveWorld.Tuning.…` |
| `static Rules? _cache; if (_cache == null) …`，靠人记得失效 | `Derived` 按来源引用自动重建 |
| 在 `Draw()` 或 UI 回调里扣金币、改实体 | UI 形成操作请求，由 Logic 校验并修改 |
| 检查里写 `Check(enemies == 8)` | 用配置公式计算期望值 |
| 工作线程直接 `sim.Remove(handle)` | 工作线程写结果区间，主线程按槽位顺序提交 |

## 参考入口

| 主题 | 入口 |
| --- | --- |
| 句柄与槽位容器 | [SlotMap.cs](../Libs/Engine/Core/Structure/SlotMap.cs)、[SlotHandle.cs](../Libs/Engine/Core/Structure/SlotHandle.cs) |
| 新项目目录、UI、发布与验证 | [新项目创建提示词](New-project.md) |
| 项目文档分类与维护 | [项目文档约定](Doc-guidelines.md) |
| Engine 能力与可选 ECS | [Engine 文档](../Libs/Engine/README.md) |
