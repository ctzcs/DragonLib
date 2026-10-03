using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DragonLib.Analyzers;

/// <summary>
/// DLWEB001: LINQ over structs defined outside the .NET framework (e.g. Foster's Color).
/// Under wasm AOT, System.Linq's generic code for such a struct may run in the interpreter while the lambda is
/// AOT-compiled; the gsharedvt bridge between them reads out of bounds ("memory access out of bounds").
/// Use a plain loop instead. Framework structs (int, Vector2, ValueTuple of those) and enums are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WasmAotLinqAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        "DLWEB001",
        "LINQ over a custom struct can crash under WebAssembly AOT",
        "Enumerable.{0} over struct '{1}' can crash under WebAssembly AOT (memory access out of bounds); use a loop",
        "DragonLib.Web",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Mono's wasm AOT runs some generic System.Linq instantiations over custom value types in the interpreter, and calling AOT-compiled lambdas from them crashes.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var method = ((IInvocationOperation)context.Operation).TargetMethod;
        if (method.ContainingType is not { Name: "Enumerable" } type || type.ContainingNamespace?.ToDisplayString() != "System.Linq")
            return;
        foreach (var argument in method.TypeArguments)
        {
            if (FindCustomStruct(argument) is { } custom)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), method.Name,
                    custom.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                return;
            }
        }
    }

    /// <summary>The first custom (non-framework, non-enum) struct in <paramref name="type"/> or its generic arguments.</summary>
    private static ITypeSymbol? FindCustomStruct(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named)
        {
            foreach (var argument in named.TypeArguments)
                if (FindCustomStruct(argument) is { } nested) return nested;
        }
        if (!type.IsValueType || type.TypeKind == TypeKind.Enum || type is ITypeParameterSymbol) return null;
        string assembly = type.ContainingAssembly?.Name ?? "";
        bool framework = assembly is "mscorlib" or "netstandard" || assembly.StartsWith("System", System.StringComparison.Ordinal)
            || assembly.StartsWith("Microsoft", System.StringComparison.Ordinal);
        return framework ? null : type;
    }
}
