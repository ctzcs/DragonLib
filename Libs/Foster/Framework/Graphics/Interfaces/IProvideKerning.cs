namespace Foster.Framework;

/// <summary>
/// Class that provides kerning data for <see cref="SpriteFont"/> 
/// </summary>
public interface IProvideKerning
{
	public float GetKerning(int codepointA, int codepointB, float size);
}