using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;
using static CiscoEndpointDocumentationApiExtractor.Txas.Commands;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public class CsClientWriter {

    private const string GENERATED_DIR = @"..\..\..\..\CSxAPI\Generated";
    private const string NAMESPACE     = "CSxAPI";
    private const string FILE_HEADER   = "// Generated file";

    private static readonly UTF8Encoding UTF8 = new(false, true);
    private static readonly string GENERATED_ATTRIBUTE = $"[GeneratedCode(\"{Assembly.GetExecutingAssembly().GetName().Name}\", \"{Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}\")]";

    /// <summary>
    /// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/
    /// </summary>
    private static readonly ISet<string> RESERVED_CS_KEYWORDS = new HashSet<string> {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
        "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    public static async Task writeClient(ExtractedDocumentation documentation) {
        await writeConfiguration(documentation);
        await writeCommands(documentation);
        await writeStatuses(documentation);
        await writeEnums(documentation);
    }

    private static StreamWriter openFileStream(string filename) {
        string filePath = Path.Combine(GENERATED_DIR, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        return new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read), UTF8);
    }

    private static async Task writeCommands(ExtractedDocumentation documentation) {
        await using StreamWriter icommandsWriter = openFileStream("ICommands.cs");
        await using StreamWriter commandsWriter  = openFileStream("Commands.cs");

        IDictionary<string, ISet<InterfaceChild>> interfaceTree = generateInterfaceTree(documentation.commands);

        await icommandsWriter.WriteAsync($"""
            {FILE_HEADER}

            using {NAMESPACE}.Enums;
            using System.CodeDom.Compiler;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await icommandsWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod { command: DocXCommand command }:
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

                    case Subinterface s:
                        await icommandsWriter.WriteAsync($"    {s.interfaceName} {s.getterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await icommandsWriter.WriteAsync("}\r\n\r\n");
        }

        await commandsWriter.WriteAsync($$"""
            {{FILE_HEADER}}

            using {{NAMESPACE}}.Enums;
            using System.CodeDom.Compiler;

            namespace {{NAMESPACE}};

            {{GENERATED_ATTRIBUTE}}
            public class Commands: {{string.Join(", ", interfaceTree.Keys)}} {

                private IXapiTransport transport;
            
            
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

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface subinterface in interfaceNode.Value.OfType<Subinterface>()) {
                await commandsWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await commandsWriter.WriteAsync("}");
    }

    private static async Task writeEnums(ExtractedDocumentation documentation) {
        await using StreamWriter enumsWriter = openFileStream("Enums\\Enums.cs");

        await enumsWriter.WriteAsync($"""
            {FILE_HEADER}

            using System.CodeDom.Compiler;
            using System.Diagnostics.CodeAnalysis;

            namespace {NAMESPACE}.Enums;

            """);

        foreach (DocXConfiguration command in documentation.commands.Concat(documentation.configurations)) {
            await enumsWriter.WriteAsync(string.Join(null, command.parameters.Where(parameter => parameter.type == DataType.ENUM).Cast<EnumParameter>().Select(parameter => {
                string enumTypeName = getEnumName(command, parameter.name);

                return $$"""

                /// <summary>For use with <see cref="{{getInterfaceName(command)}}.{{command.nameWithoutBrackets.Last()}}{{$"({string.Join(", ", command.parameters.OrderByDescending(p => p.required).Select(p => $"{p.type switch {
                    DataType.INTEGER => "int",
                    DataType.STRING  => "string",
                    DataType.ENUM    => getEnumName(command, p.name)
                }}{(p.required ? "" : "?")}"))})"}}" /></summary>
                {{GENERATED_ATTRIBUTE}}
                public enum {{enumTypeName}} {

                {{string.Join(",\r\n\r\n", parameter.possibleValues.Select(value => $"    /// <summary><para><c>{value.name}</c>{(value.description is not null ? ": " + value.description.NewLinesToParagraphs(true) : "")}</para></summary>\r\n    {xapiEnumValueToCsIdentifier(command, parameter, value)}"))}}

                }

                """;
            })));
        }

        foreach (DocXStatus xStatus in documentation.statuses.Where(status => status.returnValueSpace.type == DataType.ENUM)) {
            EnumValueSpace valueSpace   = (EnumValueSpace) xStatus.returnValueSpace;
            string         enumTypeName = getEnumName(xStatus);

            await enumsWriter.WriteAsync($$"""

                /// <summary>For use with <see cref="{{getInterfaceName(xStatus)}}.{{xStatus.nameWithoutBrackets.Last()}}{{$"({string.Join(", ", xStatus.arrayIndexParameters.Select(_ => "int"))})"}}" /></summary>
                {{GENERATED_ATTRIBUTE}}
                public enum {{enumTypeName}} {
                
                {{string.Join(",\r\n\r\n", valueSpace.possibleValues.Select(value => $"    /// <summary><para><c>{value.name}</c>{(value.description is not null ? ": " + value.description.NewLinesToParagraphs(true) : "")}</para></summary>\r\n    {xapiEnumValueToCsIdentifier(xStatus, null, value)}"))}}

                }
            
                """);
        }

        await enumsWriter.WriteAsync($$"""

            {{GENERATED_ATTRIBUTE}}
            internal static class EnumSerializer {

                private static readonly IDictionary<Type, (Func<Enum, string> serialize, Func<string, Enum> deserialize)> enumSerializers =
                new Dictionary<Type, (Func<Enum, string>, Func<string, Enum>)>();

                public static T Deserialize<T>(string serialized) where T: Enum => (T) enumSerializers[typeof(T)].deserialize(serialized);

                public static string Serialize<T>(T deserialized) where T: Enum => enumSerializers[typeof(T)].serialize(deserialized);

                private static string DefaultSerializer(Enum o) => o.ToString();

                static EnumSerializer() {

            """);

        foreach (DocXConfiguration command in documentation.commands.Concat(documentation.configurations)) {
            await enumsWriter.WriteAsync(string.Join(null, command.parameters.Where(parameter => parameter.type == DataType.ENUM).Cast<EnumParameter>().Select(parameter => {
                string enumTypeName = getEnumName(command, parameter.name);

                IEnumerable<string> serializerSwitchArms = parameter.possibleValues
                    // .Where(value => xapiEnumValueToCsIdentifier(command, parameter, value) != value.name)
                    .Select(value => $"{enumTypeName}.{xapiEnumValueToCsIdentifier(command, parameter, value)} => \"{value.name}\"")
                    .ToList();

                IEnumerable<string> deserializerSwitchArms =
                    parameter.possibleValues.Select(value => $"\"{value.name.ToLowerInvariant()}\" => {enumTypeName}.{xapiEnumValueToCsIdentifier(command, parameter, value)}");

                return $$"""
                            enumSerializers.Add(typeof({{enumTypeName}}), (
                                serialize: deserialized => ({{enumTypeName}}) deserialized switch {
                                    {{string.Join(",\r\n                ", serializerSwitchArms.Append("_ => deserialized.ToString()"))}}
                                },
                                deserialize: serialized => serialized.ToLowerInvariant() switch {
                                    {{string.Join(",\r\n                ", deserializerSwitchArms.Append($"_ => throw new ArgumentOutOfRangeException(nameof(serialized), serialized, $\"Unknown {enumTypeName} enum value {{serialized}}, known values are {{string.Join(\", \", Enum.GetValues<{enumTypeName}>())}}.\")"))}}
                                }));


                    """;

            })));
        }

        foreach (DocXStatus xStatus in documentation.statuses.Where(status => status.returnValueSpace.type == DataType.ENUM)) {
            EnumValueSpace valueSpace   = (EnumValueSpace) xStatus.returnValueSpace;
            string         enumTypeName = getEnumName(xStatus);

            IEnumerable<string> deserializerSwitchArms =
                valueSpace.possibleValues.Select(value => $"\"{value.name.ToLowerInvariant()}\" => {enumTypeName}.{xapiEnumValueToCsIdentifier(xStatus, null, value)}");

            await enumsWriter.WriteAsync($$"""
                        enumSerializers.Add(typeof({{enumTypeName}}), (
                            serialize: DefaultSerializer,
                            deserialize: serialized => serialized.ToLowerInvariant() switch {
                                {{string.Join(",\r\n                ", deserializerSwitchArms.Append($"_ => throw new ArgumentOutOfRangeException(nameof(serialized), serialized, $\"Unknown {enumTypeName} enum value {{serialized}}, known values are {{string.Join(\", \", Enum.GetValues<{enumTypeName}>())}}.\")"))}}
                            }));


                """);
        }

        await enumsWriter.WriteAsync("    }\r\n}");

        static string xapiEnumValueToCsIdentifier(AbstractCommand command, EnumParameter? parameter, EnumValue value) {
            bool   isTimeZone = command.name.SequenceEqual(new[] { "xConfiguration", "Time", "Zone" }) && parameter?.name == "Zone";
            string name       = isTimeZone ? value.name : string.Join(null, value.name.Split('-').Select(s => s.ToUpperFirstLetter()));
            name = Regex.Replace(name, @"[^a-z0-9_]", match => match.Value switch {
                "."                                               => "_",
                "/"                                               => "_",      //"Ⳇ",
                "+" when isTimeZone                               => "_Plus_", //ႵᏐǂߙƚϯᵻᵼ
                "-" when isTimeZone && value.name.Contains("GMT") => "_Minus_",
                "-" when isTimeZone                               => "_",
                _                                                 => ""
            }, RegexOptions.IgnoreCase).ToUpperFirstLetter();
            return char.IsLetter(name[0]) ? name : "_" + name;
        }
    }

    private static async Task writeConfiguration(ExtractedDocumentation documentation) {
        await using StreamWriter configurationWriter  = openFileStream("Configuration.cs");
        await using StreamWriter iconfigurationWriter = openFileStream("IConfiguration.cs");

        IDictionary<string, ISet<InterfaceChild>> interfaceTree = generateInterfaceTree(documentation.configurations);

        await iconfigurationWriter.WriteAsync($"""
            {FILE_HEADER}

            using {NAMESPACE}.Enums;
            using System.CodeDom.Compiler;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await iconfigurationWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod { command: DocXConfiguration configuration } when configuration.parameters.Any():
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

                    case Subinterface s:
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
                private EventSubscriber eventSubscriber;
            
            
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
            string readSerializedType   = configurationParameter.type == DataType.INTEGER ? "int" : "string";
            string remoteCallExpression = $"await transport.GetConfigurationOrStatus<{readSerializedType}>({path}).ConfigureAwait(false)";
            await configurationWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{getterImplementationMethod.signature}} {
                        return {{generateDeserializerExpression(configurationParameter, command, remoteCallExpression)}};
                    }

                
                """);

            await configurationWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{generateEventSignature(command, false)}} {
                        add => eventSubscriber.Subscribe<{{readSerializedType}}, {{getterImplementationMethod.returnType}}>(new[] { {{string.Join(", ", command.name.Where((s, i) => !command.parameters.Any(parameter => parameter is IntParameter { indexOfParameterInName: { } paramIndex } && paramIndex == i)).Select(s => $"\"{s}\""))}} }, value, serialized => {{generateDeserializerExpression(configurationParameter, command, "serialized")}}).Wait();
                        remove => eventSubscriber.Unsubscribe(value).Wait();
                    }


                """);
        }

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface subinterface in interfaceNode.Value.OfType<Subinterface>()) {
                await configurationWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await configurationWriter.WriteAsync("}");

        static string generateDeserializerExpression(Parameter configurationParameter, DocXConfiguration command, string remoteCallExpression) => configurationParameter.type == DataType.ENUM
            ? $"ValueSerializer.Deserialize<{getEnumName(command, configurationParameter.name)}>({remoteCallExpression})"
            : $"ValueSerializer.Deserialize({remoteCallExpression})";
    }

    private static async Task writeStatuses(ExtractedDocumentation documentation) {
        await using StreamWriter statusWriter  = openFileStream("Status.cs");
        await using StreamWriter istatusWriter = openFileStream("IStatus.cs");

        IDictionary<string, ISet<InterfaceChild>> interfaceTree = generateInterfaceTree(documentation.statuses);

        await istatusWriter.WriteAsync($"""
            {FILE_HEADER}

            using {NAMESPACE}.Enums;
            using System.CodeDom.Compiler;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await istatusWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod { command: DocXStatus status }:
                        (string signature, string returnType) methodSignature = generateMethodSignature(status, true);
                        await istatusWriter.WriteAsync($"""
                                /// <summary>
                                /// <para><c>{string.Join(' ', status.name)}</c></para>
                                /// {status.description.NewLinesToParagraphs()}
                                /// </summary>
                            {string.Join("\r\n", status.arrayIndexParameters.Select(param => $"    /// <param name=\"{getArgumentName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                                /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>)
                                {methodSignature.signature};

                                /// <summary>
                                /// <para><c>{string.Join(' ', status.name)}</c></para>
                                /// {status.description.NewLinesToParagraphs()}
                                /// <para>Fires an event when the status changes.</para>
                                /// </summary>
                                {generateEventSignature(status, true)};


                            """);
                        break;

                    case Subinterface s:
                        await istatusWriter.WriteAsync($"    {s.interfaceName} {s.getterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await istatusWriter.WriteAsync("}\r\n\r\n");
        }

        await statusWriter.WriteAsync($$"""
            {{FILE_HEADER}}

            using {{NAMESPACE}}.Enums;
            using System.CodeDom.Compiler;

            namespace {{NAMESPACE}};

            {{GENERATED_ATTRIBUTE}}
            public class Status: {{string.Join(", ", interfaceTree.Keys)}} {

                private IXapiTransport transport;
                private EventSubscriber eventSubscriber;


            """);

        foreach (DocXStatus xStatus in documentation.statuses) {
            (string signature, string returnType) getterImplementationMethod = generateMethodSignature(xStatus, false);

            string path =
                $"new[] {{ {string.Join(", ", xStatus.name.Select((s, i) => xStatus.arrayIndexParameters.FirstOrDefault(parameter => parameter.indexOfParameterInName == i) is { } pathParameter ? $"{getArgumentName(pathParameter)}.ToString()" : $"\"{s}\""))} }}";

            string serializedType = xStatus.returnValueSpace.type == DataType.INTEGER && xStatus.returnValueSpace is not IntValueSpace { optionalValue: not null } ? "int" : "string";
            string remoteCallExpression = $"await transport.GetConfigurationOrStatus<{serializedType}>({path}).ConfigureAwait(false)";
            await statusWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{getterImplementationMethod.signature}} {
                        return {{generateDeserializerExpression(xStatus, remoteCallExpression)}};
                    }


                """);

            string eventSignature = generateEventSignature(xStatus, false);
            await statusWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{eventSignature}} {
                        add => eventSubscriber.Subscribe<{{serializedType}}, {{getterImplementationMethod.returnType}}>(new[] { {{string.Join(", ", xStatus.name.Where((s, i) => !xStatus.arrayIndexParameters.Any(parameter => parameter is { indexOfParameterInName: { } paramIndex } && paramIndex == i)).Select(s => $"\"{s}\""))}} }, value, serialized => {{generateDeserializerExpression(xStatus, "serialized")}}).Wait();
                        remove => eventSubscriber.Unsubscribe(value).Wait();
                    }


                """);
        }

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface subinterface in interfaceNode.Value.OfType<Subinterface>()) {
                await statusWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await statusWriter.WriteAsync("}");

        static string generateDeserializerExpression(DocXStatus command, string remoteCallExpression) => command.returnValueSpace.type switch {
            DataType.ENUM => $"ValueSerializer.Deserialize<{getEnumName(command)}>({remoteCallExpression})",
            DataType.INTEGER when command.returnValueSpace is IntValueSpace { optionalValue: { } optionalValue } => $"ValueSerializer.Deserialize({remoteCallExpression}, \"{optionalValue}\")",
            _             => $"ValueSerializer.Deserialize({remoteCallExpression})"
        };
    }

    private static IDictionary<string, ISet<InterfaceChild>> generateInterfaceTree(IEnumerable<AbstractCommand> documentation) {
        IDictionary<string, ISet<InterfaceChild>> interfaceTree = new Dictionary<string, ISet<InterfaceChild>>();

        foreach (AbstractCommand command in documentation) {
            string childInterfaceName = getInterfaceName(command);
            putInterfaceChild(childInterfaceName, new InterfaceMethod(command));

            for (int length = command.nameWithoutBrackets.Count - 1; length > 1; length--) {
                string parentInterfaceName = getInterfaceName(new DocXStatus { name = command.nameWithoutBrackets.Take(length).ToList() });
                putInterfaceChild(parentInterfaceName, new Subinterface(childInterfaceName, command.nameWithoutBrackets[length - 1]));
                childInterfaceName = parentInterfaceName;
            }

            void putInterfaceChild(string interfaceName, InterfaceChild child) {
                if (interfaceTree.TryGetValue(interfaceName, out ISet<InterfaceChild>? entry)) {
                    entry.Add(child);
                } else {
                    interfaceTree.Add(interfaceName, new HashSet<InterfaceChild> { child });
                }
            }
        }

        return interfaceTree;
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

    private static (string signature, string returnType) generateMethodSignature(DocXStatus status, bool isInterfaceMethod) {
        string returnType = $"{status.returnValueSpace.type switch {
            DataType.INTEGER when status.returnValueSpace is IntValueSpace { optionalValue: not null } => "int?",
            DataType.INTEGER                                                                           => "int",
            DataType.STRING                                                                            => "string",
            DataType.ENUM                                                                              => getEnumName(status)
        }}";
        return (
            $"{(isInterfaceMethod ? "" : "async ")}Task<{returnType}> {(isInterfaceMethod ? "" : getInterfaceName(status) + '.')}{status.nameWithoutBrackets.Last()}({string.Join(", ", status.arrayIndexParameters.Select(parameter => $"int {getArgumentName(parameter)}"))})",
            returnType);
    }

    private static string generateEventSignature(DocXConfiguration configuration, bool isInterfaceEvent) {
        return $"event EventCallback<{configuration.parameters.Last() switch {
            { type: DataType.STRING }               => "string",
            { type: DataType.INTEGER }              => "int",
            { type: DataType.ENUM, name: var name } => getEnumName(configuration, name)
        }}> {(isInterfaceEvent ? "" : getInterfaceName(configuration) + '.')}{configuration.nameWithoutBrackets.Last()}Changed";
    }

    private static string generateEventSignature(DocXStatus status, bool isInterfaceEvent) {
        return $"event EventCallback<{status.returnValueSpace.type switch {
            DataType.INTEGER when status.returnValueSpace is IntValueSpace { optionalValue: not null } => "int?",
            DataType.INTEGER                                                                           => "int",
            DataType.STRING                                                                            => "string",
            DataType.ENUM                                                                              => getEnumName(status)
        }}> {(isInterfaceEvent ? "" : getInterfaceName(status) + '.')}{status.nameWithoutBrackets.Last()}Changed";
    }

    private static string getInterfaceName(AbstractCommand method) {
        string singular = "I" + string.Join(null, method.nameWithoutBrackets.Skip(1)
            .SkipLast(1)
            .Append(method.nameWithoutBrackets[0][1..]));
        return singular + (singular.EndsWith('s') ? "es" : "s");
    }

    private static string getEnumName(DocXStatus command) {
        return getEnumName(command, null);
    }

    private static string getEnumName(DocXConfiguration command, string parameterName) {
        return getEnumName((AbstractCommand) command, parameterName);
    }

    private static string getEnumName(AbstractCommand command, string? parameterName) {
        IEnumerable<string> segments = command.nameWithoutBrackets;
        if (parameterName != null) {
            segments = segments.Append(parameterName);
        }

        return string.Join(null, segments.DistinctConsecutive()).TrimStart('x');
    }

    private static string getArgumentName(Parameter param, bool forDocumentationXmlComment = false) {
        string name = param.name.Replace(".", null);
        name = name == name.ToUpperInvariant() ? name.ToLowerInvariant() : name.ToLowerFirstLetter();

        if (!forDocumentationXmlComment && RESERVED_CS_KEYWORDS.Contains(name.ToLowerInvariant())) {
            name = "@" + name;
        }

        return name;
    }

}