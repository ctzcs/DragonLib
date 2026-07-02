using System.Reflection;

namespace Engine;

public static class ReflectionUtility
{
    public static T? TryGetObject<T>(object source) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var type = source.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var properties = type.GetProperties(flags);
        for (int i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            if (!typeof(T).IsAssignableFrom(property.PropertyType))
                continue;
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            try
            {
                if (property.GetValue(source) is T value)
                    return value;
            }
            catch
            {
            }
        }

        var fields = type.GetFields(flags);
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (!typeof(T).IsAssignableFrom(field.FieldType))
                continue;

            if (field.GetValue(source) is T value)
                return value;
        }

        return null;
    }
}