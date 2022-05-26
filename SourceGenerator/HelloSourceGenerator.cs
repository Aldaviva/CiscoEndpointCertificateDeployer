using Microsoft.CodeAnalysis;

namespace SourceGenerator;

[Generator]
public class HelloSourceGenerator: ISourceGenerator {

    public void Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext context) {
        IMethodSymbol mainMethod   = context.Compilation.GetEntryPoint(context.CancellationToken)!;
        string        mainTypeName = mainMethod.ContainingType.Name;

        string source = $@" // Auto-generated code
using System;

namespace {mainMethod.ContainingNamespace.ToDisplayString()}
{{
    public static partial class {mainTypeName}
    {{
        static partial void HelloFrom(string name) =>
            Console.WriteLine($""Generator says: Hi from '{{name}}'"");
    }}
}}
";
        context.AddSource($"{mainTypeName}.generated.cs", source);
    }

}