using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public partial class CsClientWriter {

    private static async Task writeCommands(ExtractedDocumentation documentation) {
        await using StreamWriter icommandsWriter = openFileStream("ICommands.cs");
        await using StreamWriter commandsWriter  = openFileStream("Commands.cs");

        IDictionary<string, ISet<InterfaceChild<DocXCommand>>> interfaceTree = generateInterfaceTree(documentation.commands);

        await icommandsWriter.WriteAsync($"""
            {FILE_HEADER}

            using {NAMESPACE}.Data;
            using System.CodeDom.Compiler;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild<DocXCommand>>> interfaceNode in interfaceTree) {
            await icommandsWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild<DocXCommand> interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod<DocXCommand> { command: var command }:
                        (string signature, string returnType) methodSignature = generateMethodSignature(command, true);
                        await icommandsWriter.WriteAsync($"""
                /// <summary>
                /// <para><c>{string.Join(' ', command.name)}</c></para>
                /// {command.description.NewLinesToParagraphs()}
                /// </summary>
            {string.Join("\r\n", command.parameters.Select(param => $"    /// <param name=\"{getArgumentName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>)
                {methodSignature.signature};


            """);
                        break;

                    case Subinterface<DocXCommand> s:
                        await icommandsWriter.WriteAsync($"    {s.interfaceName} {s.getterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await icommandsWriter.WriteAsync("}\r\n\r\n");
        }

        await commandsWriter.WriteAsync($$"""
            {{FILE_HEADER}}

            using {{NAMESPACE}}.Data;
            using {{NAMESPACE}}.Serialization;
            using {{NAMESPACE}}.Transport;
            using System.CodeDom.Compiler;

            namespace {{NAMESPACE}};

            {{GENERATED_ATTRIBUTE}}
            internal class Commands: {{string.Join(", ", interfaceTree.Keys)}} {

                private readonly IXapiTransport transport;
            
            
            """);

        foreach (DocXCommand command in documentation.commands) {
            string path = $"new[] {{ {string.Join(", ", command.name.Select(s => $"\"{s}\""))} }}";
            string parameters = command.parameters.Any()
                ? $"new Dictionary<string, object?> {{ {string.Join(", ", command.parameters.Select(parameter => $"{{ \"{parameter.name}\", {(parameter.type == DataType.ENUM ? $"ValueSerializer.Serialize({getArgumentName(parameter)})" : getArgumentName(parameter))} }}"))} }}"
                : "null";

            await commandsWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{generateMethodSignature(command, false).signature}} {
                        return await transport.CallMethod({{path}}, {{parameters}}).ConfigureAwait(false);
                    }


                """);
        }

        foreach (KeyValuePair<string, ISet<InterfaceChild<DocXCommand>>> interfaceNode in interfaceTree) {
            foreach (Subinterface<DocXCommand> subinterface in interfaceNode.Value.OfType<Subinterface<DocXCommand>>()) {
                await commandsWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await commandsWriter.WriteAsync("}");
    }

    private static (string signature, string returnType) generateMethodSignature(DocXCommand command, bool isInterfaceMethod) {
        const string RETURN_TYPE = "IDictionary<string, object?>";
        return
            ($"{(isInterfaceMethod ? "" : "async ")}Task<{RETURN_TYPE}> {(isInterfaceMethod ? "" : getInterfaceName(command) + '.')}{command.nameWithoutBrackets.Last()}({string.Join(", ", command.parameters.OrderByDescending(parameter => parameter.required).Select(parameter => $"{parameter.type switch {
                DataType.INTEGER => "int",
                DataType.STRING  => "string",
                DataType.ENUM    => getEnumName(command, parameter.name)
            }}{(parameter.required ? "" : "?")} {getArgumentName(parameter)}{(parameter.required || !isInterfaceMethod ? "" : " = null")}"))})", RETURN_TYPE);
    }

}