using System.Runtime.InteropServices;

namespace Foster.Framework;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct TextureSampler(
	TextureFilter Filter,
	TextureWrap WrapX,
	TextureWrap WrapY,
	bool Mipmaps = false) // DragonLib 扩展：默认关闭，保持现有 2D sampler 行为。
{
	public TextureSampler(TextureFilter filter, TextureWrap wrapXY)
		: this(filter, wrapXY, wrapXY) {}
}
