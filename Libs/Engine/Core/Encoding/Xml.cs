using System.Xml;
using System.Xml.Serialization;
using Foster.Framework;

namespace Engine;

public static class Xml
{
    private static readonly XmlWriterSettings _DefaultSettings = new()
    {
        Indent = true,
    };

    /// <summary>
    /// 默认RootName在%AppData%\GameName\中
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static T LoadXml<T>(this FileSystem fileSystem, string path)
    {
        using var storage = fileSystem.OpenUserStorageAsync().GetAwaiter().GetResult();
        var text = storage.ReadAllText(path);
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(text);
        return (T?)serializer.Deserialize(reader)
            ?? throw new InvalidOperationException($"Failed to deserialize XML from '{path}'.");
    }

    /// <summary>
    /// 默认RootName在%AppData%\GameName\中
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="path"></param>
    /// <param name="xmlFile"></param>
    /// <typeparam name="T"></typeparam>
    public static void SaveXml<T>(this FileSystem fileSystem, string path, T xmlFile)
    {
        using var storage = fileSystem.OpenUserStorageAsync().GetAwaiter().GetResult();
        storage.SaveXml(path, xmlFile);
    }

    public static void SaveXml<T>(this StorageContainer storage, string path, T xmlFile)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            storage.CreateDirectory(directory);

        var serializer = new XmlSerializer(typeof(T));
        using var stringWriter = new StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, _DefaultSettings))
        {
            serializer.Serialize(xmlWriter, xmlFile);
        }
        storage.WriteAllText(path, stringWriter.ToString());
    }

    public static T LoadXml<T>(this StorageContainer storage, string path)
    {
        var text = storage.ReadAllText(path);
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(text);
        return (T?)serializer.Deserialize(reader)
            ?? throw new InvalidOperationException($"Failed to deserialize XML from '{path}'.");
    }
}
