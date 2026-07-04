
using System.Text.Json;
using cfg;
using Foster.Framework;

public static class ConfigLoader
{
    /// <summary>相对于传入的 Storage 根的数据目录（使用正斜杠，SDL Storage 与 System.IO 均兼容）。</summary>
    public static string Path { get; set; } = "Resources/Luban/Data/json";

    public static Tables? Tables { get; private set; }

    public static void LoadTables(StorageContainer storage)
    {
        Tables = new Tables(file =>
        {
            using var stream = storage.OpenRead($"{Path}/{file}.json");
            return JsonDocument.Parse(stream).RootElement;
        });
    }
}