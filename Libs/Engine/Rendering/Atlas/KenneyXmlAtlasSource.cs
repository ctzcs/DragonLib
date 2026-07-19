using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Foster.Framework;

namespace Engine;

/// <summary>
/// Kenney / Starling 风格 XML 图集来源：
/// <c>&lt;TextureAtlas&gt;&lt;SubTexture name x y width height/&gt;…&lt;/TextureAtlas&gt;</c>。
/// <para>会为每个子图额外收一份去掉 “.png” 后缀的别名，调用处写短名更省事。</para>
/// </summary>
public sealed class KenneyXmlAtlasSource : ISpriteAtlasSource
{
    private readonly string _xmlPath;

    public KenneyXmlAtlasSource(string xmlPath) => _xmlPath = xmlPath;

    public IReadOnlyDictionary<string, RectInt> BuildRects(LocalStorage storage, int atlasWidth, int atlasHeight)
    {
        using var xmlStream = storage.OpenRead(_xmlPath);
        var serializer = new XmlSerializer(typeof(AtlasXml));
        var atlas = (AtlasXml?)serializer.Deserialize(xmlStream)
                    ?? throw new IOException($"解析图集 XML 失败：{_xmlPath}");

        var rects = new Dictionary<string, RectInt>();
        foreach (var s in atlas.SubTextures)
        {
            var rect = new RectInt(s.X, s.Y, s.Width, s.Height);
            rects[s.Name] = rect;
            if (s.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                rects[s.Name[..^4]] = rect;    // 去后缀别名
        }
        return rects;
    }

    /// <summary>便捷入口：<c>SpriteAtlas.Load(dev, storage, png, new KenneyXmlAtlasSource(xml))</c> 的简写。</summary>
    public static SpriteAtlas Load(GraphicsDevice device, LocalStorage storage, string pngPath, string xmlPath)
        => SpriteAtlas.Load(device, storage, pngPath, new KenneyXmlAtlasSource(xmlPath));

    // ---- Kenney/Starling XML 反序列化模型 ----
    [XmlRoot("TextureAtlas")]
    public sealed class AtlasXml
    {
        [XmlElement("SubTexture")]
        public List<SubTextureXml> SubTextures { get; set; } = new();
    }

    public sealed class SubTextureXml
    {
        [XmlAttribute("name")] public string Name { get; set; } = "";
        [XmlAttribute("x")] public int X { get; set; }
        [XmlAttribute("y")] public int Y { get; set; }
        [XmlAttribute("width")] public int Width { get; set; }
        [XmlAttribute("height")] public int Height { get; set; }
    }
}
