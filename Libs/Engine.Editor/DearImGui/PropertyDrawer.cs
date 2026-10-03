
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using ImGuiNET;

namespace Engine.DearImGui;

public interface IPropertyDrawer
{
    Type? TargetType { get; }
    bool CanHandle(Type type);
    bool Draw(string label, ref object? value, Type declaredType);
}

public abstract class PropertyDrawer<T> : IPropertyDrawer
{
    public virtual Type? TargetType => typeof(T);
    public virtual bool CanHandle(Type type) => type == typeof(T);

    public bool Draw(string label, ref object? value, Type declaredType)
    {
        T typed = value is T t ? t : default!;
        bool changed = OnDraw(label, ref typed);
        if (changed) value = typed;
        return changed;
    }

    protected abstract bool OnDraw(string label, ref T value);
}

public static class PropertyDrawerRegistry
{
    private static readonly Dictionary<Type, IPropertyDrawer> _byType = new();
    private static readonly List<IPropertyDrawer> _generic = new();
    private static bool _initialized;

    public static void Register(IPropertyDrawer drawer)
    {
        if (drawer.TargetType != null)
            _byType[drawer.TargetType] = drawer;
        else
            _generic.Add(drawer);
    }

    public static void Register<T>(PropertyDrawer<T> drawer) => Register((IPropertyDrawer)drawer);

    public static IPropertyDrawer? Find(Type type)
    {
        EnsureInitialized();
        if (_byType.TryGetValue(type, out var d)) return d;
        for (int i = 0; i < _generic.Count; i++)
            if (_generic[i].CanHandle(type)) return _generic[i];
        return null;
    }

    public static bool DrawField(string label, ref object? value, Type declaredType)
    {
        var drawer = Find(declaredType);
        if (drawer != null)
            return drawer.Draw(label, ref value, declaredType);
        return ReflectionStructDrawer.DrawStructInline(label, ref value, declaredType);
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        DefaultPropertyDrawers.RegisterAll();
    }
}

public static class ReflectionStructDrawer
{
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> _cache = new();

    private static FieldInfo[] GetFields(Type type) =>
        _cache.GetOrAdd(type, t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));

    public static bool DrawStructBody(ref object? value, Type type)
    {
        if (value == null)
        {
            ImGui.TextDisabled("(null)");
            return false;
        }
        var fields = GetFields(type);
        if (fields.Length == 0)
        {
            ImGui.TextDisabled(value.ToString() ?? "(empty)");
            return false;
        }
        bool changed = false;
        for (int i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            object? fv = f.GetValue(value);
            if (PropertyDrawerRegistry.DrawField(f.Name, ref fv, f.FieldType))
            {
                f.SetValue(value, fv);
                changed = true;
            }
        }
        return changed;
    }

    public static bool DrawStructInline(string label, ref object? value, Type type)
    {
        bool changed = false;
        ImGui.PushID(label);
        if (ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            changed = DrawStructBody(ref value, type);
            ImGui.TreePop();
        }
        ImGui.PopID();
        return changed;
    }
}


public static class DefaultPropertyDrawers
{
    public static void RegisterAll()
    {
        PropertyDrawerRegistry.Register(new IntDrawer());
        PropertyDrawerRegistry.Register(new UIntDrawer());
        PropertyDrawerRegistry.Register(new FloatDrawer());
        PropertyDrawerRegistry.Register(new DoubleDrawer());
        PropertyDrawerRegistry.Register(new BoolDrawer());
        PropertyDrawerRegistry.Register(new StringDrawer());
        PropertyDrawerRegistry.Register(new Vector2Drawer());
        PropertyDrawerRegistry.Register(new Vector3Drawer());
        PropertyDrawerRegistry.Register(new Vector4Drawer());
        PropertyDrawerRegistry.Register(new ByteDrawer());
        PropertyDrawerRegistry.Register(new SByteDrawer());
        PropertyDrawerRegistry.Register(new ShortDrawer());
        PropertyDrawerRegistry.Register(new UShortDrawer());
        PropertyDrawerRegistry.Register(new LongDrawer());
        PropertyDrawerRegistry.Register(new EnumDrawer());
    }
}

public sealed class IntDrawer : PropertyDrawer<int>
{
    protected override bool OnDraw(string label, ref int v) => ImGui.DragInt(label, ref v);
}

public sealed class UIntDrawer : PropertyDrawer<uint>
{
    protected override bool OnDraw(string label, ref uint v)
    {
        int i = (int)v;
        var c = ImGui.DragInt(label, ref i, 1f, 0, int.MaxValue);
        if (c) v = (uint)Math.Max(0, i);
        return c;
    }
}

public sealed class FloatDrawer : PropertyDrawer<float>
{
    protected override bool OnDraw(string label, ref float v) => ImGui.DragFloat(label, ref v, 0.1f);
}

public sealed class DoubleDrawer : PropertyDrawer<double>
{
    protected override bool OnDraw(string label, ref double v)
    {
        float f = (float)v;
        var c = ImGui.DragFloat(label, ref f, 0.1f);
        if (c) v = f;
        return c;
    }
}

public sealed class BoolDrawer : PropertyDrawer<bool>
{
    protected override bool OnDraw(string label, ref bool v) => ImGui.Checkbox(label, ref v);
}

public sealed class StringDrawer : PropertyDrawer<string>
{
    protected override bool OnDraw(string label, ref string v)
    {
        v ??= string.Empty;
        return ImGui.InputText(label, ref v, 512);
    }
}

public sealed class Vector2Drawer : PropertyDrawer<Vector2>
{
    protected override bool OnDraw(string label, ref Vector2 v) => ImGui.DragFloat2(label, ref v, 0.1f);
}

public sealed class Vector3Drawer : PropertyDrawer<Vector3>
{
    protected override bool OnDraw(string label, ref Vector3 v) => ImGui.DragFloat3(label, ref v, 0.1f);
}

public sealed class Vector4Drawer : PropertyDrawer<Vector4>
{
    protected override bool OnDraw(string label, ref Vector4 v) => ImGui.DragFloat4(label, ref v, 0.1f);
}

public sealed class ByteDrawer : PropertyDrawer<byte>
{
    protected override bool OnDraw(string label, ref byte v)
    {
        int i = v;
        var c = ImGui.DragInt(label, ref i, 1f, 0, 255);
        if (c) v = (byte)Math.Clamp(i, 0, 255);
        return c;
    }
}

public sealed class SByteDrawer : PropertyDrawer<sbyte>
{
    protected override bool OnDraw(string label, ref sbyte v)
    {
        int i = v;
        var c = ImGui.DragInt(label, ref i, 1f, sbyte.MinValue, sbyte.MaxValue);
        if (c) v = (sbyte)Math.Clamp(i, sbyte.MinValue, sbyte.MaxValue);
        return c;
    }
}

public sealed class ShortDrawer : PropertyDrawer<short>
{
    protected override bool OnDraw(string label, ref short v)
    {
        int i = v;
        var c = ImGui.DragInt(label, ref i, 1f, short.MinValue, short.MaxValue);
        if (c) v = (short)Math.Clamp(i, short.MinValue, short.MaxValue);
        return c;
    }
}

public sealed class UShortDrawer : PropertyDrawer<ushort>
{
    protected override bool OnDraw(string label, ref ushort v)
    {
        int i = v;
        var c = ImGui.DragInt(label, ref i, 1f, 0, ushort.MaxValue);
        if (c) v = (ushort)Math.Clamp(i, 0, ushort.MaxValue);
        return c;
    }
}

public sealed class LongDrawer : PropertyDrawer<long>
{
    protected override bool OnDraw(string label, ref long v)
    {
        string s = v.ToString();
        if (ImGui.InputText(label, ref s, 32, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (long.TryParse(s, out var parsed)) { v = parsed; return true; }
        }
        return false;
    }
}

public sealed class EnumDrawer : IPropertyDrawer
{
    public Type? TargetType => null;
    public bool CanHandle(Type type) => type.IsEnum;

    public bool Draw(string label, ref object? value, Type declaredType)
    {
        var names = Enum.GetNames(declaredType);
        int cur = value == null ? 0 : Math.Max(0, Array.IndexOf(names, value.ToString()));
        if (ImGui.Combo(label, ref cur, names, names.Length))
        {
            value = Enum.Parse(declaredType, names[cur]);
            return true;
        }
        return false;
    }
}


