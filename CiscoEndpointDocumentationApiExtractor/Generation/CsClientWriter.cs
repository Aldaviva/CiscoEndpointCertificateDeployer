using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public class CsClientWriter {

    private const string GENERATED_DIR = @"..\..\..\..\CSxAPI\Generated";
    private const string NAMESPACE     = "CSxAPI";

    private static readonly string GENERATED_ATTRIBUTE = $"[GeneratedCode(\"{Assembly.GetExecutingAssembly().GetName().Name}\", \"{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}\")]";

    private const string FILE_HEADER = "// Generated file";

    /// <summary>
    /// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/
    /// </summary>
    private static readonly ISet<string> RESERVED_CS_KEYWORDS = new HashSet<string> {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
        "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    private static readonly UTF8Encoding UTF8 = new(false, true);

    public async Task writeClient(ExtractedDocumentation documentation) {
        await writeConfiguration(documentation);
        await writeCommands(documentation);
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

            using System.CodeDom.Compiler;
            using System.Xml.Linq;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await icommandsWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod { command: DocXCommand command }:
                        await icommandsWriter.WriteAsync($"""
                /// <summary>
                /// <para><c>{string.Join(' ', command.name)}</c></para>
                /// {command.description.NewLinesToParagraphs()}
                /// </summary>
            {string.Join("\r\n", command.parameters.Select(param => $"    /// <param name=\"{getArgumentName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>
                {generateMethodSignature(command, true)};


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
            using System.Xml.Linq;

            namespace {{NAMESPACE}};

            {{GENERATED_ATTRIBUTE}}
            public class Commands: {{string.Join(", ", interfaceTree.Keys)}} {

                private IXapiTransport transport;
            
            
            """);

        foreach (DocXCommand command in documentation.commands) {

            await commandsWriter.WriteAsync("    /// <inheritdoc />\r\n    ");
            await commandsWriter.WriteAsync(generateMethodSignature(command, false));

            string path = $"new[] {{ {string.Join(", ", command.name.Select(s => $"\"{s}\""))} }}";
            string parameters = command.parameters.Any()
                ? $"new Dictionary<string, object?> {{ {string.Join(", ", command.parameters.Select(parameter => $"{{ \"{parameter.name}\", {(parameter.type == DataType.ENUM ? $"EnumSerializer.Serialize({getArgumentName(parameter)})" : getArgumentName(parameter))} }}"))} }}"
                : "null";

            await commandsWriter.WriteAsync($$"""
                 {
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

                /// <summary>For use with <see cref="{{getInterfaceName(command)}}.{{command.nameWithoutParameters.Last()}}{{(command is DocXCommand ? "" : $"({string.Join(", ", command.parameters.Select(p => $"{p.type switch {
                    DataType.INTEGER => "int",
                    DataType.STRING  => "string",
                    DataType.ENUM    => getEnumName(command, p.name)
                }}"))})")}}" /></summary>
                {{GENERATED_ATTRIBUTE}}
                public enum {{enumTypeName}} {

                {{string.Join(",\r\n\r\n", parameter.possibleValues.Select(value => $"    /// <summary><para><c>{value.name}</c>{(value.description is not null ? ": " + value.description.NewLinesToParagraphs(true) : "")}</para></summary>\r\n    {xapiEnumValueToCsIdentifier(command, parameter, value)}"))}}

                }

                """;
            })));
        }

        await enumsWriter.WriteAsync($$"""
            {{GENERATED_ATTRIBUTE}}
            public static class EnumSerializer {

                public static string? Serialize(Enum? defaultToStringEnum) => defaultToStringEnum?.ToString();


            """);

        foreach (DocXConfiguration command in documentation.commands.Concat(documentation.configurations)) {
            await enumsWriter.WriteAsync(string.Join(null, command.parameters.Where(parameter => parameter.type == DataType.ENUM).Cast<EnumParameter>().Select(parameter => {
                string enumTypeName = getEnumName(command, parameter.name);

                IEnumerable<string> serializerSwitchArms = parameter.possibleValues
                    .Where(value => xapiEnumValueToCsIdentifier(command, parameter, value) != value.name)
                    .Select(value => $"{enumTypeName}.{xapiEnumValueToCsIdentifier(command, parameter, value)} => \"{value.name}\"")
                    .ToList();

                string? serializerExpression = serializerSwitchArms.Any()
                    ? $$"""
                        deserialized switch {
                                {{string.Join(",\r\n        ", serializerSwitchArms.Append("_ => deserialized.ToString()"))}}
                            }
                        """
                    : null;

                IEnumerable<string> deserializerSwitchArms =
                    parameter.possibleValues.Select(value => $"\"{value.name.ToLowerInvariant()}\" => {enumTypeName}.{xapiEnumValueToCsIdentifier(command, parameter, value)}");

                return $$"""
                    {{(serializerExpression is not null ? $"public static string? Serialize({enumTypeName}? deserialized) => {serializerExpression};" : "")}}

                    public static bool TryDeserialize(string serialized, [NotNullWhen(true)] out {{enumTypeName}}? deserialized) {
                        deserialized = serialized.ToLowerInvariant() switch {
                            {{string.Join(",\r\n            ", deserializerSwitchArms.Append("_ => null"))}}
                        };
                        return deserialized != null;
                    }


                """;
            })));
        }

        await enumsWriter.WriteAsync("}");

        static string xapiEnumValueToCsIdentifier(DocXConfiguration command, EnumParameter parameter, EnumValue value) {
            bool   isTimeZone = command.name.SequenceEqual(new[] { "xConfiguration", "Time", "Zone" }) && parameter.name == "Zone";
            string name       = isTimeZone ? value.name : string.Join(null, value.name.Split('-').Select(s => s.ToUpperFirstLetter()));
            name = Regex.Replace(name, @"[^a-z0-9_]", match => match.Value switch {
                "."                                               => "_",
                "/"                                               => "_",      //"Ⳇ",
                "+" when isTimeZone                               => "_Plus_", //ႵᏐǂߙƚϯᵻᵼ
                "-" when isTimeZone && value.name.Contains("GMT") => "_Minus_",
                "-" when isTimeZone                               => "_",
                _                                                 => string.Empty
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

            using System.CodeDom.Compiler;
            using System.Xml.Linq;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await iconfigurationWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod { command: DocXConfiguration configuration } when configuration.parameters.Any():
                        await iconfigurationWriter.WriteAsync($"""
                                /// <summary>
                                /// <para><c>{string.Join(' ', configuration.name)}</c></para>
                                /// {configuration.description.NewLinesToParagraphs()}
                                /// </summary>
                            {string.Join("\r\n", configuration.parameters.Select(param => $"    /// <param name=\"{getArgumentName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                                /// <returns>A <see cref="Task"/> that will complete asynchronously when the configuration change has been received by the device.</returns>
                                {generateMethodSignature(configuration, true, true)};

                                /// <summary>
                                /// <para><c>{string.Join(' ', configuration.name)}</c></para>
                                /// {configuration.description.NewLinesToParagraphs()}
                                /// </summary>
                                /// <returns>A <see cref="Task&lt;String&gt;"/> that will complete asynchronously with the response from the device.</returns>
                                {generateMethodSignature(configuration, false, true)};


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
            using System.Xml.Linq;

            namespace {{NAMESPACE}};

            {{GENERATED_ATTRIBUTE}}
            public class Configuration: {{string.Join(", ", interfaceTree.Keys)}} {

                private IXapiTransport transport;
            
            
            """);

        foreach (DocXConfiguration command in documentation.configurations.Where(configuration => configuration.parameters.Any())) {
            Parameter configurationParameter = command.parameters.Last();

            await configurationWriter.WriteAsync("    /// <inheritdoc />\r\n    ");
            await configurationWriter.WriteAsync(generateMethodSignature(command, true, false));

            string path =
                $"new[] {{ {string.Join(", ", command.name.Select((s, i) => command.parameters.OfType<IntParameter>().FirstOrDefault(parameter => parameter.indexOfParameterInName == i) is { } pathParameter ? $"{getArgumentName(pathParameter)}.ToString()" : $"\"{s}\""))} }}";

            await configurationWriter.WriteAsync($$"""
                 {
                        await transport.SetConfiguration({{path}}, {{getArgumentName(configurationParameter)}}).ConfigureAwait(false);
                    }


                """);

            await configurationWriter.WriteAsync("    /// <inheritdoc />\r\n    ");
            await configurationWriter.WriteAsync(generateMethodSignature(command, false, false));

            string autoDeserializedType = configurationParameter.type == DataType.INTEGER ? "int" : "string";
            string remoteCallExpression = $"await transport.GetConfigurationOrStatus<{autoDeserializedType}>({path}).ConfigureAwait(false)";
            await configurationWriter.WriteAsync(" {\r\n");

            await configurationWriter.WriteAsync(configurationParameter.type == DataType.ENUM
                ? $$"""
                        string serialized = {{remoteCallExpression}};
                        return EnumSerializer.TryDeserialize(serialized, out {{NAMESPACE}}.Enums.{{getEnumName(command, configurationParameter.name)}}? deserialized)
                            ? deserialized.Value
                            : throw new ArgumentOutOfRangeException($"Failed to deserialize {{getEnumName(command, configurationParameter.name)}} enum value from string \"{serialized}\" from response to {{string.Join(' ', command.name)}}");
                """
                : $"return {remoteCallExpression};");

            await configurationWriter.WriteAsync("\r\n    }\r\n\r\n    ");

        }

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface subinterface in interfaceNode.Value.OfType<Subinterface>()) {
                await configurationWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await configurationWriter.WriteAsync("}");
    }

    private static IDictionary<string, ISet<InterfaceChild>> generateInterfaceTree(IEnumerable<AbstractCommand> documentation) {
        IDictionary<string, ISet<InterfaceChild>> interfaceTree = new Dictionary<string, ISet<InterfaceChild>>();

        foreach (AbstractCommand command in documentation) {
            string childInterfaceName = getInterfaceName(command);
            putInterfaceChild(childInterfaceName, new InterfaceMethod(command));

            for (int length = command.nameWithoutParameters.Count - 1; length > 1; length--) {
                string parentInterfaceName = getInterfaceName(new DocXStatus { name = command.nameWithoutParameters.Take(length).ToList() });
                putInterfaceChild(parentInterfaceName, new Subinterface(childInterfaceName, command.nameWithoutParameters[length - 1]));
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

    private static string generateMethodSignature(DocXCommand command, bool isInterfaceMethod) =>
        $"{(isInterfaceMethod ? string.Empty : "async ")}Task<XElement> {(isInterfaceMethod ? string.Empty : getInterfaceName(command) + '.')}{command.nameWithoutParameters.Last()}({string.Join(", ", command.parameters.OrderByDescending(parameter => parameter.required).Select(parameter => $"{parameter.type switch {
            DataType.INTEGER => "int",
            DataType.STRING  => "string",
            DataType.ENUM    => NAMESPACE + ".Enums." + getEnumName(command, parameter.name)
        }}{(parameter.required ? string.Empty : "?")} {getArgumentName(parameter)}{(parameter.required || !isInterfaceMethod ? string.Empty : " = null")}"))})";

    private static string generateMethodSignature(DocXConfiguration configuration, bool isSetter, bool isInterfaceMethod) =>
        $"{(isInterfaceMethod ? string.Empty : "async ")}Task{(isSetter ? "" : "<"+configuration.parameters.Last() switch {
            { type: DataType.STRING }               => "string",
            { type: DataType.INTEGER }              => "int",
            { type: DataType.ENUM, name: var name } => $"{NAMESPACE}.Enums.{getEnumName(configuration, name)}"
        }+">")} {(isInterfaceMethod ? string.Empty : getInterfaceName(configuration) + '.')}{configuration.nameWithoutParameters.Last()}({string.Join(", ", configuration.parameters.SkipLast(isSetter ? 0 : 1).Select(parameter => $"{parameter.type switch {
            DataType.INTEGER => "int",
            DataType.STRING  => "string",
            DataType.ENUM    => $"{NAMESPACE}.Enums.{getEnumName(configuration, parameter.name)}"
        }} {getArgumentName(parameter)}"))})";

    private static string getInterfaceName(AbstractCommand method) {
        return "I" + string.Join(null, method.nameWithoutParameters.Skip(1)
            .SkipLast(1)
            .Append(method.nameWithoutParameters[0][1..] + 's')); //will need to be "es" for Statuses
    }

    private static string getEnumName(AbstractCommand command, string parameterName) {
        return string.Join(null, command.nameWithoutParameters.Skip(1).Append(parameterName).DistinctConsecutive());
    }

    private static string getArgumentName(Parameter param, bool forDocumentationXmlComment = false) {
        string name = param.name.Replace(".", null);
        name = name == name.ToUpperInvariant() ? name.ToLowerInvariant() : name.ToLowerFirstLetter();

        if (!forDocumentationXmlComment && RESERVED_CS_KEYWORDS.Contains(name.ToLowerInvariant())) {
            name = "@" + name;
        }

        return name;
    }

    private interface InterfaceChild { }

    private class InterfaceMethod: InterfaceChild {

        public InterfaceMethod(AbstractCommand command) {
            this.command = command;
        }

        public AbstractCommand command { get; }

        private bool Equals(InterfaceMethod other) {
            return command.Equals(other.command);
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InterfaceMethod) obj);
        }

        public override int GetHashCode() {
            return command.GetHashCode();
        }

        public static bool operator ==(InterfaceMethod? left, InterfaceMethod? right) {
            return Equals(left, right);
        }

        public static bool operator !=(InterfaceMethod? left, InterfaceMethod? right) {
            return !Equals(left, right);
        }

    }

    private class Subinterface: InterfaceChild {

        public Subinterface(string interfaceName, string getterName) {
            this.interfaceName = interfaceName;
            this.getterName    = getterName;
        }

        public string interfaceName { get; }
        public string getterName { get; }

        private bool Equals(Subinterface other) {
            return interfaceName == other.interfaceName;
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Subinterface) obj);
        }

        public override int GetHashCode() {
            return interfaceName.GetHashCode();
        }

        public static bool operator ==(Subinterface? left, Subinterface? right) {
            return Equals(left, right);
        }

        public static bool operator !=(Subinterface? left, Subinterface? right) {
            return !Equals(left, right);
        }

    }

}