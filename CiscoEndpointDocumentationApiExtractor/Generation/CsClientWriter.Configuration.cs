using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public partial class CsClientWriter {

    private static async Task writeConfiguration(ExtractedDocumentation documentation) {
        await using StreamWriter configurationWriter  = openFileStream("Configuration.cs");
        await using StreamWriter iconfigurationWriter = openFileStream("IConfiguration.cs");

        IDictionary<string, ISet<InterfaceChild<DocXConfiguration>>> interfaceTree = generateInterfaceTree(documentation.configurations);

        await iconfigurationWriter.WriteAsync($"""
            {FILE_HEADER}

            using {NAMESPACE}.Data;
            using System.CodeDom.Compiler;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild<DocXConfiguration>>> interfaceNode in interfaceTree) {
            await iconfigurationWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild<DocXConfiguration> interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod<DocXConfiguration> { command: var configuration } when configuration.parameters.Any():
                        (string signature, string returnType) setterSignature = generateMethodSignature(configuration, true, true);
                        (string signature, string returnType) getterSignature = generateMethodSignature(configuration, false, true);
                        await iconfigurationWriter.WriteAsync($"""
                                /// <summary>
                                /// <para><c>{string.Join(' ', configuration.name)}</c></para>
                                /// {configuration.description.NewLinesToParagraphs()}
                                /// </summary>
                            {string.Join("\r\n", configuration.parameters.Select(param => $"    /// <param name=\"{getArgumentName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                                /// <returns>A <see cref="Task"/> that will complete asynchronously when the configuration change has been received by the device.</returns>)
                                {setterSignature.signature};

                                /// <summary>
                                /// <para><c>{string.Join(' ', configuration.name)}</c></para>
                                /// {configuration.description.NewLinesToParagraphs()}
                                /// </summary>
                            {string.Join("\r\n", configuration.parameters.Where(parameter => parameter is IntParameter { indexOfParameterInName: not null }).Select(param => $"    /// <param name=\"{getArgumentName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                                /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>
                                {getterSignature.signature};

                                /// <summary>
                                /// <para><c>{string.Join(' ', configuration.name)}</c></para>
                                /// {configuration.description.NewLinesToParagraphs()}
                                /// <para>Fires an event when the configuration changes.</para>
                                /// </summary>
                                {generateEventSignature(configuration, true)};

                            """);
                        break;

                    case Subinterface<DocXConfiguration> s:
                        await iconfigurationWriter.WriteAsync($"    {s.interfaceName} {s.getterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await iconfigurationWriter.WriteAsync("}\r\n\r\n");
        }

        await configurationWriter.WriteAsync($$"""
            {{FILE_HEADER}}

            using {{NAMESPACE}}.Enums;
            using System.CodeDom.Compiler;

            namespace {{NAMESPACE}};

            {{GENERATED_ATTRIBUTE}}
            public class Configuration: {{string.Join(", ", interfaceTree.Keys)}} {

                private IXapiTransport transport;
                private FeedbackSubscriber feedbackSubscriber;
            
            
            """);

        foreach (DocXConfiguration command in documentation.configurations.Where(configuration => configuration.parameters.Any())) {
            Parameter configurationParameter = command.parameters.Last();

            string path =
                $"new[] {{ {string.Join(", ", command.name.Select((s, i) => command.parameters.OfType<IntParameter>().FirstOrDefault(parameter => parameter.indexOfParameterInName == i) is { } pathParameter ? $"{getArgumentName(pathParameter)}.ToString()" : $"\"{s}\""))} }}";

            await configurationWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{generateMethodSignature(command, true, false).signature}} {
                        await transport.SetConfiguration({{path}}, ValueSerializer.Serialize({{getArgumentName(configurationParameter)}})).ConfigureAwait(false);
                    }

                
                """);

            (string signature, string returnType) getterImplementationMethod = generateMethodSignature(command, false, false);
            string                                readSerializedType         = configurationParameter.type == DataType.INTEGER ? "int" : "string";
            string                                remoteCallExpression       = $"await transport.GetConfigurationOrStatus<{readSerializedType}>({path}).ConfigureAwait(false)";
            await configurationWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{getterImplementationMethod.signature}} {
                        return {{generateDeserializerExpression(configurationParameter, command, remoteCallExpression)}};
                    }

                
                """);

            await configurationWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{generateEventSignature(command, false)}} {
                        add => feedbackSubscriber.Subscribe<{{readSerializedType}}, {{getterImplementationMethod.returnType}}>(new[] { {{string.Join(", ", command.name.Where((s, i) => !command.parameters.Any(parameter => parameter is IntParameter { indexOfParameterInName: { } paramIndex } && paramIndex == i)).Select(s => $"\"{s}\""))}} }, value, serialized => {{generateDeserializerExpression(configurationParameter, command, "serialized")}}).Wait();
                        remove => feedbackSubscriber.Unsubscribe(value).Wait();
                    }


                """);
        }

        foreach (KeyValuePair<string, ISet<InterfaceChild<DocXConfiguration>>> interfaceNode in interfaceTree) {
            foreach (Subinterface<DocXConfiguration> subinterface in interfaceNode.Value.OfType<Subinterface<DocXConfiguration>>()) {
                await configurationWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await configurationWriter.WriteAsync("}");

        static string generateDeserializerExpression(Parameter configurationParameter, DocXConfiguration command, string remoteCallExpression) => configurationParameter.type == DataType.ENUM
            ? $"ValueSerializer.Deserialize<{getEnumName(command, configurationParameter.name)}>({remoteCallExpression})"
            : $"ValueSerializer.Deserialize({remoteCallExpression})";
    }

    private static (string signature, string returnType) generateMethodSignature(DocXConfiguration configuration, bool isSetter, bool isInterfaceMethod) {
        string returnType = isSetter ? "" : configuration.parameters.Last() switch {
            { type: DataType.STRING }               => "string",
            { type: DataType.INTEGER }              => "int",
            { type: DataType.ENUM, name: var name } => getEnumName(configuration, name)
        };
        return (
            $"{(isInterfaceMethod ? "" : "async ")}{(returnType == "" ? "Task" : $"Task<{returnType}>")} {(isInterfaceMethod ? "" : getInterfaceName(configuration) + '.')}{configuration.nameWithoutBrackets.Last()}({string.Join(", ", configuration.parameters.SkipLast(isSetter ? 0 : 1).Select(parameter => $"{parameter.type switch {
                DataType.INTEGER => "int",
                DataType.STRING  => "string",
                DataType.ENUM    => getEnumName(configuration, parameter.name)
            }} {getArgumentName(parameter)}"))})", returnType);
    }

    private static string generateEventSignature(DocXConfiguration configuration, bool isInterfaceEvent) {
        return $"event FeedbackCallback<{configuration.parameters.Last() switch {
            { type: DataType.STRING }               => "string",
            { type: DataType.INTEGER }              => "int",
            { type: DataType.ENUM, name: var name } => getEnumName(configuration, name)
        }}> {(isInterfaceEvent ? "" : getInterfaceName(configuration) + '.')}{configuration.nameWithoutBrackets.Last()}Changed";
    }


}