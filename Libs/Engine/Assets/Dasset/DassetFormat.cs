namespace Engine.Assets.Dasset;

/// <summary>
/// .dasset 文件格式常量。布局（little-endian）：
/// Header(magic + version) → 贴图表 → [v2] 骨架表 → primitives(顶点/索引/AABB/材质) → 模型级 AABB
/// → [v2] 动画剪辑表。v2 的 primitive 多一个顶点布局标记（0 静态 / 1 蒙皮），顶点块按布局定步长。
/// 读写实现见 <see cref="DassetWriter"/> / <see cref="DassetReader"/>。
/// </summary>
public static class DassetFormat
{
    /// <summary>文件头魔数，按 little-endian 写出即 ASCII "DAST"。</summary>
    public const uint Magic = 0x54534144;

    /// <summary>当前格式版本。布局变更时 bump，并同步升级 Reader/Writer；Reader 兼容 v1。</summary>
    public const int Version = 4; // v3：PBR 材质；v4：channel 插值类型及 cubic in/value/out 三元组。
}

/// <summary>顶点布局标记（随 v2 的 primitive 写入文件）。</summary>
public enum DassetVertexLayout
{
    /// <summary>PositionNormalUvVertex（48B，静态，节点变换已烘焙）。</summary>
    PositionNormalUv = 0,

    /// <summary>PositionNormalUvSkinVertex（68B，蒙皮，顶点在 mesh bind 空间）。</summary>
    PositionNormalUvSkin = 1,
}

/// <summary>材质透明模式（与 glTF alphaMode 对齐）。渲染端：Opaque/Mask 走不透明队列（Mask 由 shader clip），Blend 走透明队列。</summary>
public enum DassetAlphaMode
{
    Opaque = 0,
    Mask = 1,
    Blend = 2,
}

/// <summary>贴图字节编码。新版 cooker 将 JPEG 转 PNG；Jpg 值保留以读取旧资产数据。</summary>
public enum DassetTextureCodec
{
    Png = 0,
    Jpg = 1,
}
