using System.Text.Json;
using Xunit;

namespace Engine.Tests;

public sealed class DependencyTests
{
    [Fact]
    public void EngineOnlyApplicationDoesNotIncludeEcsDependencies()
    {
        string dependencyFile = Path.ChangeExtension(typeof(DependencyTests).Assembly.Location, ".deps.json");
        using var dependencies = JsonDocument.Parse(File.ReadAllText(dependencyFile));
        string[] libraries = dependencies.RootElement.GetProperty("libraries")
            .EnumerateObject().Select(library => library.Name).ToArray();

        Assert.Contains(libraries, library => library.StartsWith("Engine/", StringComparison.Ordinal));
        Assert.DoesNotContain(libraries, library =>
            library.StartsWith("DragonECS", StringComparison.OrdinalIgnoreCase)
            || library.StartsWith("Engine.ECS/", StringComparison.OrdinalIgnoreCase));
    }
}
