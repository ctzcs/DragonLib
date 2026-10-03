namespace Foster.Framework;

/// <summary>
/// Optional Texture Flags used when creating new Textures
/// </summary>
[Flags]
public enum TextureFlags
{
	None = 0,

	/// <summary>
	/// Allows the Texture to be used during Compute Storage Reads
	/// </summary>
	ComputeRead = 1 << 0,

	/// <summary>
	/// Allows the Texture to be used during Compute Storage Writes
	/// </summary>
	ComputeWrite = 1 << 1,

	// DragonLib 扩展：采样颜色纹理上传后生成完整 mip 链。
	GenerateMipmaps = 1 << 2,
}
