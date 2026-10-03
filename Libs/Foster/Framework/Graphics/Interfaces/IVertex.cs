namespace Foster.Framework;

/// <summary>
/// Provides a Vertex struct format information to be used in a <see cref="VertexBuffer"/>
/// </summary>
public interface IVertex
{
	/// <summary>
	/// Gets the Format of the Vertex.<br/>
	/// This should return a static value, not create a new format every time it's accessed.
	/// </summary>
	public VertexFormat Format { get; }
}