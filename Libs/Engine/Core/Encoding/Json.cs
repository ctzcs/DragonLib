using System.Text.Json;
using Foster.Framework;

namespace Engine;

public static class Json
{
    private static readonly JsonSerializerOptions _DefaultOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    /// <summary>
    /// 默认RootName在%AppData%\GameName\中
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="path"></param>
    /// <param name="options"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static T LoadJson<T>(this FileSystem fileSystem, string path, JsonSerializerOptions? options = null )
    {
        using var storage = fileSystem.OpenUserStorageAsync().GetAwaiter().GetResult();
        var text = storage.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(text, options??_DefaultOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize JSON from '{path}'.");
    }

    /// <summary>
    /// 默认RootName在%AppData%\GameName\中
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="path"></param>
    /// <param name="jsonFile"></param>
    /// <param name="options"></param>
    /// <typeparam name="T"></typeparam>
    public static void SaveJson<T>(this FileSystem fileSystem, string path, T jsonFile, JsonSerializerOptions? options = null )
    {
        using var storage = fileSystem.OpenUserStorageAsync().GetAwaiter().GetResult();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            storage.CreateDirectory(directory);

        var text = JsonSerializer.Serialize(jsonFile, options??_DefaultOptions);
        storage.WriteAllText(path, text);
    }

    public static void SaveJson<T>(this StorageContainer storage, string path, T jsonFile, JsonSerializerOptions? options = null)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            storage.CreateDirectory(directory);
        var text = JsonSerializer.Serialize(jsonFile, options ?? _DefaultOptions);
        storage.WriteAllText(path, text);
    }

    public static T LoadJson<T>(this StorageContainer storage, string path, JsonSerializerOptions? options = null)
    {
        var text = storage.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(text, options ?? _DefaultOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize JSON from '{path}'.");
    }
}
