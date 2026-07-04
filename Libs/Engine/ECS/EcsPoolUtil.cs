using System.Collections.Generic;
using System.Reflection;
using DCFApixels.DragonECS;
using DCFApixels.DragonECS.Core;

namespace Engine.ECS;

/// <summary>
/// 运行期按 <see cref="System.Type"/> 存取组件的反射工具。DebugInspector 与预制体 / 关卡
/// 序列化共用：两者都需要"给任意类型的组件拿到对应 pool"以及"按类型名找回组件类型"。
/// </summary>
public static class EcsPoolUtil
{
    private static readonly Dictionary<Type, MethodInfo> _getPoolInstanceCache = new();
    private static readonly MethodInfo _getPoolInstanceOpen =
        typeof(EcsWorld).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == "GetPoolInstance" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

    /// <summary>
    /// 拿到（必要时创建）某组件类型在该 world 里的 pool。会依据组件实现的接口
    /// （Tag / Value / 普通）选择正确的 pool 类型。
    /// </summary>
    public static IEcsPool? GetOrCreatePoolFor(EcsWorld world, Type componentType)
    {
        Type poolType;
        if (typeof(IEcsTagComponent).IsAssignableFrom(componentType))
            poolType = typeof(EcsTagPool<>).MakeGenericType(componentType);
        else if (typeof(IEcsValueComponent).IsAssignableFrom(componentType))
            poolType = typeof(EcsValuePool<>).MakeGenericType(componentType);
        else
            poolType = typeof(EcsPool<>).MakeGenericType(componentType);

        if (!_getPoolInstanceCache.TryGetValue(poolType, out var mi))
        {
            mi = _getPoolInstanceOpen.MakeGenericMethod(poolType);
            _getPoolInstanceCache[poolType] = mi;
        }
        return mi.Invoke(world, null) as IEcsPool;
    }

    // 组件类型名（Type.Name）→ Type。用于反序列化时按名字找回组件类型。
    private static Dictionary<string, Type>? _byName;

    /// <summary>按 <see cref="Type.Name"/> 解析组件类型。找不到返回 null。</summary>
    public static Type? ResolveComponentType(string name)
    {
        if (_byName == null)
        {
            var map = new Dictionary<string, Type>(256);
            foreach (var t in ComponentTypeCatalog.All)
                map[t.Name] = t; // 同名冲突时后者覆盖；组件名一般唯一
            _byName = map;
        }
        return _byName.TryGetValue(name, out var type) ? type : null;
    }

    /// <summary>组件目录变化后（如热重载）清空名字缓存。</summary>
    public static void InvalidateCatalog()
    {
        _byName = null;
        ComponentTypeCatalog.Invalidate();
    }

    /// <summary>所有已发现的组件类型（供编辑器"添加组件"下拉枚举）。ComponentTypeCatalog 是内部类型，这里对外暴露。</summary>
    public static IReadOnlyList<Type> AllComponentTypes => ComponentTypeCatalog.All;

    /// <summary>
    /// 给实体添加一个指定类型的空组件（按 Tag / Value / 普通选对 pool）。已存在则不动。
    /// 失败只告警不抛，供编辑器 UI 安全调用。
    /// </summary>
    public static void AddEmptyComponent(EcsWorld world, int entity, Type componentType)
    {
        try
        {
            var pool = GetOrCreatePoolFor(world, componentType);
            if (pool == null)
            {
                EcsDebug.PrintWarning($"Add component {componentType.Name} failed: cannot resolve pool.");
                return;
            }
            if (!pool.Has(entity))
                pool.AddEmpty(entity);
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            EcsDebug.PrintWarning($"Add component {componentType.Name} failed: {tie.InnerException.Message}");
        }
        catch (Exception ex)
        {
            EcsDebug.PrintWarning($"Add component {componentType.Name} failed: {ex.Message}");
        }
    }
}
