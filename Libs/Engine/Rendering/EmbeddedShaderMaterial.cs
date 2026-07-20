using System.Reflection;
using Foster.Framework;

namespace Engine.Rendering;

public readonly record struct ShaderStageSpec(
    int SamplerCount,
    int UniformBufferCount,
    string EntryPoint);

/// <summary>
/// Owns a material whose shaders are loaded from embedded, driver-specific resources.
/// Resource names follow: {baseName}.{stage}.{dxil|spv|msl}.
/// </summary>
/// <remarks>
/// Configure the embedded resource names in the consuming project, for example:
/// <code>
/// &lt;EmbeddedResource Include="$(ProjectDir)Shaders/Compiled/**"&gt;
///   &lt;LogicalName&gt;Game0/Shaders/%(Filename)%(Extension)&lt;/LogicalName&gt;
/// &lt;/EmbeddedResource&gt;
/// </code>
/// A physical file named <c>Shaders/Compiled/DreamBlock.fragment.dxil</c> then has the
/// manifest resource name <c>Game0/Shaders/DreamBlock.fragment.dxil</c>. Pass
/// <c>Game0/Shaders/DreamBlock</c> as <paramref name="resourceBaseName"/>; this loader
/// appends the shader stage and current graphics-driver extension.
/// Use <see cref="Assembly.GetManifestResourceNames"/> to inspect the final names.
/// </remarks>
public sealed class EmbeddedShaderMaterial : IDisposable
{
    private readonly Shader? _vertexShader;
    private readonly Shader _fragmentShader;
    private bool _disposed;

    public Material Material { get; }

    private EmbeddedShaderMaterial(Shader? vertexShader, Shader fragmentShader)
    {
        _vertexShader = vertexShader;
        _fragmentShader = fragmentShader;
        Material = new Material(vertexShader, fragmentShader);
    }

    /// <param name="resourceBaseName">
    /// Manifest resource name without the stage and driver extension, such as
    /// <c>Game0/Shaders/DreamBlock</c>. This is a logical resource name configured by
    /// MSBuild's <c>LogicalName</c> (in csproj), not necessarily the physical folder path.
    /// </param>
    public static EmbeddedShaderMaterial Load(
        GraphicsDevice device,
        Assembly assembly,
        string resourceBaseName,
        ShaderStageSpec fragment,
        ShaderStageSpec? vertex = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceBaseName);
        Validate(fragment, nameof(fragment));
        if (vertex.HasValue)
            Validate(vertex.Value, nameof(vertex));

        var extension = device.Driver.GetShaderExtension();
        if (string.IsNullOrWhiteSpace(extension))
            throw new NotSupportedException($"Graphics driver '{device.Driver}' has no shader resource extension.");

        var fragmentResource = $"{resourceBaseName}.fragment.{extension}";
        var fragmentCode = ReadResource(assembly, fragmentResource, device.Driver);

        byte[]? vertexCode = null;
        string? vertexResource = null;
        if (vertex.HasValue)
        {
            vertexResource = $"{resourceBaseName}.vertex.{extension}";
            vertexCode = ReadResource(assembly, vertexResource, device.Driver);
        }

        Shader? vertexShader = null;
        Shader? fragmentShader = null;
        try
        {
            if (vertex.HasValue)
            {
                var spec = vertex.Value;
                vertexShader = new Shader(
                    device,
                    ShaderStage.Vertex,
                    vertexCode!,
                    samplerCount: spec.SamplerCount,
                    uniformBufferCount: spec.UniformBufferCount,
                    entryPoint: spec.EntryPoint,
                    name: vertexResource);
            }

            fragmentShader = new Shader(
                device,
                ShaderStage.Fragment,
                fragmentCode,
                samplerCount: fragment.SamplerCount,
                uniformBufferCount: fragment.UniformBufferCount,
                entryPoint: fragment.EntryPoint,
                name: fragmentResource);

            return new EmbeddedShaderMaterial(vertexShader, fragmentShader);
        }
        catch
        {
            vertexShader?.Dispose();
            fragmentShader?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _vertexShader?.Dispose();
        _fragmentShader.Dispose();
    }

    private static void Validate(in ShaderStageSpec spec, string parameterName)
    {
        if (spec.SamplerCount < 0)
            throw new ArgumentOutOfRangeException(parameterName, "Sampler count cannot be negative.");
        if (spec.UniformBufferCount < 0)
            throw new ArgumentOutOfRangeException(parameterName, "Uniform buffer count cannot be negative.");
        if (string.IsNullOrWhiteSpace(spec.EntryPoint))
            throw new ArgumentException("Shader entry point cannot be empty.", parameterName);
    }

    private static byte[] ReadResource(Assembly assembly, string resourceName, GraphicsDriver driver)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            var available = string.Join(", ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Embedded shader resource '{resourceName}' was not found for graphics driver '{driver}' " +
                $"in assembly '{assembly.GetName().Name}'. Available resources: [{available}]");
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
