namespace Foster.Framework;

/// <summary>Foster 0.4 content storage backed by preloaded assets and browser localStorage.</summary>
public sealed class ContentStorage : Storage
{
    private ContentStorage(string prefix, bool writable) : base(prefix, writable) { }

    internal new static ContentStorage OpenUserStorage(string name)
        => new($"foster:{Uri.EscapeDataString(name)}:", true);

    public new static ContentStorage OpenTitleStorage(string? path)
        => new(string.IsNullOrEmpty(path) ? "" : Normalize(path) + "/", false);
}
