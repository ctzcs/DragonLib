using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

// Managed single-file C# port of Ravn's entities.odin.
// Requires C# 7.3 or newer because component accessors use ref returns.
//
// Example:
//
// public struct Named
// {
//     public string Name;
// }
//
// public struct Foo
// {
//     public Named Named;
//     public int Value;
// }
//
// public struct Bar
// {
//     public Named Named;
//     public float Speed;
// }
//
// public struct Baz
// {
//     public int Value;
// }
//
// var entities = new Entities(defaultCapacity: 4096);
// entities.Register<Foo>(64);
// entities.Register<Bar>(64);
// entities.Register<Baz>(64);
//
// Marking Named with [EntitiesComponent] makes Register<Foo> and Register<Bar>
// discover and register those fields automatically. Components without the
// attribute can still be registered manually.
//
// EntityHandle foo = entities.Create(new Foo {
//     Named = new Named { Name = "First" },
//     Value = 10,
// });
//
// entities.ForEach<Foo>((EntityHandle handle, ref Foo value) => {
//     value.Value++;
// });
//
// entities.ForEachComponent<Named>((EntityHandle handle, ref Named named) => {
//     named.Name += "!";
// });

[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class EntitiesComponentAttribute : Attribute
{
}

public readonly struct EntityHandle : IEquatable<EntityHandle>
{
    public readonly int Index;
    public readonly uint Generation;
    public readonly int Variant;

    public EntityHandle(int index, uint generation, int variant)
    {
        Index = index;
        Generation = generation;
        Variant = variant;
    }

    public bool IsNone
    {
        get { return Index == 0; }
    }

    public static EntityHandle None
    {
        get { return default(EntityHandle); }
    }

    public bool Equals(EntityHandle other)
    {
        return Index == other.Index
            && Generation == other.Generation
            && Variant == other.Variant;
    }

    public override bool Equals(object obj)
    {
        return obj is EntityHandle && Equals((EntityHandle)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Index;
            hash = (hash * 397) ^ (int)Generation;
            hash = (hash * 397) ^ Variant;
            return hash;
        }
    }

    public override string ToString()
    {
        return IsNone
            ? "EntityHandle(None)"
            : "EntityHandle(variant=" + Variant
                + ", index=" + Index
                + ", gen=" + Generation + ")";
    }

    public static bool operator ==(EntityHandle left, EntityHandle right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EntityHandle left, EntityHandle right)
    {
        return !left.Equals(right);
    }
}

public delegate void EntityRefAction<T>(EntityHandle handle, ref T value);

public delegate ref TComponent EntityComponentAccessor<TEntity, TComponent>(
    ref TEntity entity);

/// <summary>
/// Fixed-capacity, type-separated entity storage backed entirely by managed
/// arrays. Entity metadata is stored separately from entity values so T may
/// contain managed references.
/// </summary>
public sealed class Entities
{
    private static readonly ConcurrentDictionary<Type, Action<Entities>[]>
        AutomaticComponentRegistrations =
            new ConcurrentDictionary<Type, Action<Entities>[]>();

    private static readonly MethodInfo CreateAutomaticRegistrationMethod =
        typeof(Entities).GetMethod(
            nameof(CreateAutomaticRegistration),
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "Could not find the automatic component registration factory.");

    private interface IEntityPool
    {
        int Variant { get; }
        Type EntityType { get; }
        int Count { get; }
        int Capacity { get; }

        bool IsValid(EntityHandle handle);
        bool Destroy(EntityHandle handle);
        void Clear();
    }

    private sealed class EntityPool<T> : IEntityPool
    {
        private readonly T[] _values;
        private readonly uint[] _generations;
        private readonly int[] _nextFree;
        private readonly bool[] _alive;

        private int _top;
        private int _freeHead;
        private int _count;

        public EntityPool(int variant, int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            Variant = variant;
            Capacity = capacity;

            // Slot zero is reserved, making default(EntityHandle) invalid.
            int arrayLength = checked(capacity + 1);
            _values = new T[arrayLength];
            _generations = new uint[arrayLength];
            _nextFree = new int[arrayLength];
            _alive = new bool[arrayLength];
        }

        public int Variant { get; private set; }

        public Type EntityType
        {
            get { return typeof(T); }
        }

        public int Count
        {
            get { return _count; }
        }

        public int Capacity { get; private set; }

        public bool TryAdd(T value, out EntityHandle handle)
        {
            int index;

            if (_freeHead != 0)
            {
                index = _freeHead;
                _freeHead = _nextFree[index];
                _nextFree[index] = 0;
            }
            else
            {
                if (_top >= Capacity)
                {
                    handle = default(EntityHandle);
                    return false;
                }

                index = ++_top;
                _generations[index] = 1;
            }

            _values[index] = value;
            _alive[index] = true;
            _count++;

            handle = new EntityHandle(index, _generations[index], Variant);
            return true;
        }

        public bool Destroy(EntityHandle handle)
        {
            if (!IsValid(handle))
                return false;

            int index = handle.Index;
            _values[index] = default(T);
            _alive[index] = false;
            _generations[index] = NextGeneration(_generations[index]);
            _nextFree[index] = _freeHead;
            _freeHead = index;
            _count--;
            return true;
        }

        public bool IsValid(EntityHandle handle)
        {
            return handle.Variant == Variant
                && handle.Index > 0
                && handle.Index <= _top
                && _alive[handle.Index]
                && _generations[handle.Index] == handle.Generation;
        }

        public bool TryGet(EntityHandle handle, out T value)
        {
            if (IsValid(handle))
            {
                value = _values[handle.Index];
                return true;
            }

            value = default(T);
            return false;
        }

        public ref T GetRef(EntityHandle handle)
        {
            if (!IsValid(handle))
                throw new InvalidOperationException("The entity handle is invalid or no longer alive.");

            return ref _values[handle.Index];
        }

        public void ForEach(EntityRefAction<T> action)
        {
            for (int index = 1; index <= _top; index++)
            {
                if (!_alive[index])
                    continue;

                EntityHandle handle = new EntityHandle(
                    index,
                    _generations[index],
                    Variant);

                action(handle, ref _values[index]);
            }
        }

        public void ForEachComponent<TComponent>(
            EntityComponentAccessor<T, TComponent> accessor,
            EntityRefAction<TComponent> action)
        {
            for (int index = 1; index <= _top; index++)
            {
                if (!_alive[index])
                    continue;

                EntityHandle handle = new EntityHandle(
                    index,
                    _generations[index],
                    Variant);

                ref TComponent component = ref accessor(ref _values[index]);
                action(handle, ref component);
            }
        }

        public T[] DangerousGetBuffer(out int highWaterMark)
        {
            highWaterMark = _top;
            return _values;
        }

        public EntityHandle GetHandleAt(int index)
        {
            if (index <= 0 || index > _top || !_alive[index])
                return default(EntityHandle);

            return new EntityHandle(index, _generations[index], Variant);
        }

        public void Clear()
        {
            // Clearing values is important when T contains managed references.
            Array.Clear(_values, 0, _top + 1);
            Array.Clear(_generations, 0, _top + 1);
            Array.Clear(_nextFree, 0, _top + 1);
            Array.Clear(_alive, 0, _top + 1);

            _top = 0;
            _freeHead = 0;
            _count = 0;
        }
    }

    private interface IComponentBinding<TComponent>
    {
        int Variant { get; }
        Type EntityType { get; }

        bool IsValid(EntityHandle handle);
        ref TComponent GetRef(EntityHandle handle);
        void ForEach(EntityRefAction<TComponent> action);
    }

    private sealed class ComponentBinding<TEntity, TComponent>
        : IComponentBinding<TComponent>
    {
        private readonly EntityPool<TEntity> _pool;
        private readonly EntityComponentAccessor<TEntity, TComponent> _accessor;

        public ComponentBinding(
            EntityPool<TEntity> pool,
            EntityComponentAccessor<TEntity, TComponent> accessor)
        {
            _pool = pool;
            _accessor = accessor;
        }

        public int Variant
        {
            get { return _pool.Variant; }
        }

        public Type EntityType
        {
            get { return typeof(TEntity); }
        }

        public bool IsValid(EntityHandle handle)
        {
            return _pool.IsValid(handle);
        }

        public ref TComponent GetRef(EntityHandle handle)
        {
            ref TEntity entity = ref _pool.GetRef(handle);
            return ref _accessor(ref entity);
        }

        public void ForEach(EntityRefAction<TComponent> action)
        {
            _pool.ForEachComponent(_accessor, action);
        }
    }

    private sealed class ComponentRegistry<TComponent>
    {
        public readonly Dictionary<int, IComponentBinding<TComponent>> ByVariant =
            new Dictionary<int, IComponentBinding<TComponent>>();

        public readonly List<IComponentBinding<TComponent>> Bindings =
            new List<IComponentBinding<TComponent>>();

        public void Add(IComponentBinding<TComponent> binding)
        {
            ByVariant.Add(binding.Variant, binding);
            Bindings.Add(binding);
        }
    }

    private readonly int _defaultCapacity;
    private readonly Dictionary<Type, int> _variants = new Dictionary<Type, int>();
    private readonly List<IEntityPool> _pools = new List<IEntityPool>();
    private readonly Dictionary<Type, object> _componentRegistries =
        new Dictionary<Type, object>();

    private int _count;
    private int _iterationDepth;

    public Entities(int defaultCapacity = 4096)
    {
        if (defaultCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(defaultCapacity));

        _defaultCapacity = defaultCapacity;
    }

    public int Count
    {
        get { return _count; }
    }

    public int RegisteredTypeCount
    {
        get { return _pools.Count; }
    }

    public void Register<T>(int capacity)
    {
        EnsureNotIterating("register an entity type");

        if (_variants.ContainsKey(typeof(T)))
            throw new InvalidOperationException(typeof(T).Name + " is already registered.");

        // Build and validate accessors before mutating this container. The
        // generated delegates are cached globally for every entity type.
        Action<Entities>[] automaticRegistrations =
            GetAutomaticComponentRegistrations(typeof(T));

        int variant = _pools.Count;
        var pool = new EntityPool<T>(variant, capacity);
        _variants.Add(typeof(T), variant);
        _pools.Add(pool);

        for (int i = 0; i < automaticRegistrations.Length; i++)
            automaticRegistrations[i](this);
    }

    public void Register<T>()
    {
        Register<T>(_defaultCapacity);
    }

    public void RegisterComponent<TEntity, TComponent>(
        EntityComponentAccessor<TEntity, TComponent> accessor)
    {
        if (accessor == null)
            throw new ArgumentNullException(nameof(accessor));

        EnsureNotIterating("register a component");

        EntityPool<TEntity> pool = GetOrCreatePool<TEntity>();
        ComponentRegistry<TComponent> registry = GetOrCreateRegistry<TComponent>();

        if (registry.ByVariant.ContainsKey(pool.Variant))
        {
            throw new InvalidOperationException(
                typeof(TEntity).Name + " already has a registered "
                + typeof(TComponent).Name + " component.");
        }

        registry.Add(new ComponentBinding<TEntity, TComponent>(pool, accessor));
    }

    public bool TryCreate<T>(T value, out EntityHandle handle)
    {
        EnsureNotIterating("create an entity");

        EntityPool<T> pool = GetOrCreatePool<T>();
        if (!pool.TryAdd(value, out handle))
            return false;

        _count++;
        return true;
    }

    public EntityHandle Create<T>(T value)
    {
        EntityHandle handle;
        if (!TryCreate(value, out handle))
        {
            throw new InvalidOperationException(
                "The " + typeof(T).Name + " entity pool is full.");
        }

        return handle;
    }

    public bool Destroy(EntityHandle handle)
    {
        EnsureNotIterating("destroy an entity");

        if (handle.Variant < 0 || handle.Variant >= _pools.Count)
            return false;

        if (!_pools[handle.Variant].Destroy(handle))
            return false;

        _count--;
        return true;
    }

    public bool IsValid(EntityHandle handle)
    {
        return handle.Variant >= 0
            && handle.Variant < _pools.Count
            && _pools[handle.Variant].IsValid(handle);
    }

    public bool TryGet<T>(EntityHandle handle, out T value)
    {
        EntityPool<T> pool;
        if (!TryGetPool(out pool) || handle.Variant != pool.Variant)
        {
            value = default(T);
            return false;
        }

        return pool.TryGet(handle, out value);
    }

    public T Get<T>(EntityHandle handle)
    {
        return GetRef<T>(handle);
    }

    public ref T GetRef<T>(EntityHandle handle)
    {
        EntityPool<T> pool;
        if (!TryGetPool(out pool) || handle.Variant != pool.Variant)
        {
            throw new InvalidOperationException(
                "The handle does not refer to a " + typeof(T).Name + " entity.");
        }

        return ref pool.GetRef(handle);
    }

    public bool HasComponent<TComponent>(EntityHandle handle)
    {
        ComponentRegistry<TComponent> registry;
        IComponentBinding<TComponent> binding;

        return TryGetRegistry(out registry)
            && registry.ByVariant.TryGetValue(handle.Variant, out binding)
            && binding.IsValid(handle);
    }

    public bool TryGetComponent<TComponent>(
        EntityHandle handle,
        out TComponent value)
    {
        ComponentRegistry<TComponent> registry;
        IComponentBinding<TComponent> binding;

        if (TryGetRegistry(out registry)
            && registry.ByVariant.TryGetValue(handle.Variant, out binding)
            && binding.IsValid(handle))
        {
            value = binding.GetRef(handle);
            return true;
        }

        value = default(TComponent);
        return false;
    }

    public ref TComponent GetComponentRef<TComponent>(EntityHandle handle)
    {
        ComponentRegistry<TComponent> registry;
        IComponentBinding<TComponent> binding;

        if (!TryGetRegistry(out registry)
            || !registry.ByVariant.TryGetValue(handle.Variant, out binding))
        {
            throw new InvalidOperationException(
                "The entity variant has no registered "
                + typeof(TComponent).Name + " component.");
        }

        return ref binding.GetRef(handle);
    }

    public int CountOf<T>()
    {
        EntityPool<T> pool;
        return TryGetPool(out pool) ? pool.Count : 0;
    }

    public int CapacityOf<T>()
    {
        EntityPool<T> pool;
        return TryGetPool(out pool) ? pool.Capacity : 0;
    }

    public void ForEach<T>(EntityRefAction<T> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        EntityPool<T> pool;
        if (!TryGetPool(out pool))
            return;

        _iterationDepth++;
        try
        {
            pool.ForEach(action);
        }
        finally
        {
            _iterationDepth--;
        }
    }

    public void ForEachComponent<TComponent>(EntityRefAction<TComponent> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        ComponentRegistry<TComponent> registry;
        if (!TryGetRegistry(out registry))
            return;

        _iterationDepth++;
        try
        {
            for (int i = 0; i < registry.Bindings.Count; i++)
                registry.Bindings[i].ForEach(action);
        }
        finally
        {
            _iterationDepth--;
        }
    }

    /// <summary>
    /// Returns the actual managed value array for a concrete entity type.
    /// Slot zero is reserved. Slots 1 through highWaterMark can contain holes;
    /// call GetHandleAt to determine whether a slot is alive. Do not replace
    /// array elements while an Entities iteration is active.
    /// </summary>
    public T[] DangerousGetBuffer<T>(out int highWaterMark)
    {
        EntityPool<T> pool;
        if (!TryGetPool(out pool))
        {
            highWaterMark = 0;
            return null;
        }

        return pool.DangerousGetBuffer(out highWaterMark);
    }

    public EntityHandle GetHandleAt<T>(int index)
    {
        EntityPool<T> pool;
        return TryGetPool(out pool)
            ? pool.GetHandleAt(index)
            : default(EntityHandle);
    }

    /// <summary>
    /// Clears every pool while retaining registrations and allocated arrays.
    /// Generations intentionally reset, matching the requested semantics.
    /// </summary>
    public void Clear()
    {
        EnsureNotIterating("clear the entity container");

        for (int i = 0; i < _pools.Count; i++)
            _pools[i].Clear();

        _count = 0;
    }

    private EntityPool<T> GetOrCreatePool<T>()
    {
        EntityPool<T> pool;
        if (TryGetPool(out pool))
            return pool;

        Register<T>();
        return (EntityPool<T>)_pools[_variants[typeof(T)]];
    }

    private bool TryGetPool<T>(out EntityPool<T> pool)
    {
        int variant;
        if (_variants.TryGetValue(typeof(T), out variant))
        {
            pool = (EntityPool<T>)_pools[variant];
            return true;
        }

        pool = null;
        return false;
    }

    private ComponentRegistry<TComponent> GetOrCreateRegistry<TComponent>()
    {
        ComponentRegistry<TComponent> registry;
        if (TryGetRegistry(out registry))
            return registry;

        registry = new ComponentRegistry<TComponent>();
        _componentRegistries.Add(typeof(TComponent), registry);
        return registry;
    }

    private bool TryGetRegistry<TComponent>(
        out ComponentRegistry<TComponent> registry)
    {
        object raw;
        if (_componentRegistries.TryGetValue(typeof(TComponent), out raw))
        {
            registry = (ComponentRegistry<TComponent>)raw;
            return true;
        }

        registry = null;
        return false;
    }

    private void EnsureNotIterating(string operation)
    {
        if (_iterationDepth != 0)
        {
            throw new InvalidOperationException(
                "Cannot " + operation + " while an Entities iteration is active.");
        }
    }

    private static Action<Entities>[] GetAutomaticComponentRegistrations(
        Type entityType)
    {
        return AutomaticComponentRegistrations.GetOrAdd(
            entityType,
            BuildAutomaticComponentRegistrations);
    }

    private static Action<Entities>[] BuildAutomaticComponentRegistrations(
        Type entityType)
    {
        FieldInfo[] fields = entityType.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var registrations = new List<Action<Entities>>();
        var componentFields = new Dictionary<Type, FieldInfo>();

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            Type componentType = field.FieldType;

            if (!componentType.IsDefined(
                    typeof(EntitiesComponentAttribute),
                    inherit: false))
            {
                continue;
            }

            if (field.IsInitOnly)
            {
                throw new InvalidOperationException(
                    entityType.Name + "." + field.Name
                    + " cannot be registered as " + componentType.Name
                    + " because readonly component fields cannot return a writable reference.");
            }

            FieldInfo? existingField;
            if (componentFields.TryGetValue(componentType, out existingField))
            {
                throw new InvalidOperationException(
                    entityType.Name + " contains multiple " + componentType.Name
                    + " component fields (" + existingField!.Name + " and "
                    + field.Name + ").");
            }

            componentFields.Add(componentType, field);
        }

        if (componentFields.Count == 0)
            return Array.Empty<Action<Entities>>();

        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new PlatformNotSupportedException(
                "Automatic [EntitiesComponent] registration requires dynamic code. "
                + "Register component accessors manually on AOT platforms.");
        }

        foreach (KeyValuePair<Type, FieldInfo> pair in componentFields)
        {
            MethodInfo factory = CreateAutomaticRegistrationMethod.MakeGenericMethod(
                entityType,
                pair.Key);
            object? result = factory.Invoke(
                null,
                new object[] { pair.Value });
            if (result is not Action<Entities> registration)
            {
                throw new InvalidOperationException(
                    "Could not create an automatic component registration for "
                    + entityType.Name + "." + pair.Value.Name + ".");
            }

            registrations.Add(registration);
        }

        return registrations.ToArray();
    }

    private static Action<Entities> CreateAutomaticRegistration<
        TEntity,
        TComponent>(FieldInfo field)
    {
        var accessorMethod = new DynamicMethod(
            "Get_" + typeof(TEntity).Name + "_" + field.Name,
            typeof(TComponent).MakeByRefType(),
            new[] { typeof(TEntity).MakeByRefType() },
            typeof(Entities).Module,
            skipVisibility: true);

        ILGenerator il = accessorMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);

        if (!typeof(TEntity).IsValueType)
            il.Emit(OpCodes.Ldind_Ref);

        il.Emit(OpCodes.Ldflda, field);
        il.Emit(OpCodes.Ret);

        var accessor = (EntityComponentAccessor<TEntity, TComponent>)
            accessorMethod.CreateDelegate(
                typeof(EntityComponentAccessor<TEntity, TComponent>));

        return entities =>
            entities.RegisterComponent<TEntity, TComponent>(accessor);
    }

    private static uint NextGeneration(uint generation)
    {
        unchecked
        {
            generation++;
        }

        return generation == 0 ? 1u : generation;
    }
}
