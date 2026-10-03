using System.Runtime.InteropServices;

namespace Foster.Framework;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct TextureSampler(
	TextureFilter Filter,
	TextureWrap WrapX,
	TextureWrap WrapY,
	bool Mipmaps = false)
{
	public TextureSampler(TextureFilter filter, TextureWrap wrapXY)
		: this(filter, wrapXY, wrapXY) {}
}
