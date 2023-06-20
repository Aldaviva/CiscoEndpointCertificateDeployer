using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public class CsClientWriter {

    private const string GENERATED_DIR = @"..\..\..\..\CSxAPI\Generated";
    private const string NAMESPACE     = "CSxAPI";

    private const string FILE_HEADER = """
        // Generated file


        """;

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

    private static StreamWriter openFileStream(string filename) => new(new FileStream(Path.Combine(GENERATED_DIR, filename), FileMode.Create, FileAccess.Write, FileShare.Read), UTF8);

    private static async Task writeCommands(ExtractedDocumentation documentation) {
        await using StreamWriter icommandsWriter = openFileStream("ICommands.cs");
        await using StreamWriter commandsWriter  = openFileStream("Commands.cs");

        IDictionary<string, ISet<InterfaceChild>> interfaceTree = generateInterfaceTree(documentation.commands);

        await icommandsWriter.WriteAsync(FILE_HEADER);
        await icommandsWriter.WriteAsync($"""
            using System.Xml.Linq;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await icommandsWriter.WriteAsync($"public interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod { command: DocXCommand command }:
                        await icommandsWriter.WriteAsync($"""
                /// <summary>
                /// {command.description.NewLinesToParagraphs()}
                /// </summary>
            {string.Join("\r\n", command.parameters.Select(param => $"    /// <param name=\"{getParameterName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
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

        await commandsWriter.WriteAsync(FILE_HEADER);
        await commandsWriter.WriteAsync($$"""
            using System.Xml.Linq;

            namespace {{NAMESPACE}};

            public class Commands: {{string.Join(", ", interfaceTree.Keys)}} {

                private XapiTransport transport;
            
            
            """);

        foreach (DocXCommand command in documentation.commands) {

            await commandsWriter.WriteAsync("    /// <inheritdoc />\r\n    ");
            await commandsWriter.WriteAsync(generateMethodSignature(command, false));

            string path = $"new[] {{ {string.Join(", ", command.name.Select(s => $"\"{s}\""))} }}";
            string parameters = command.parameters.Any()
                ? $"new Dictionary<string, object?> {{ {string.Join(", ", command.parameters.Select(parameter => $"{{ \"{parameter.name}\", {getParameterName(parameter)}{(parameter.type == DataType.ENUM ? (parameter.required ? string.Empty : "?") + ".ToXapiString()" : string.Empty)} }}"))} }}"
                : "null";

            await commandsWriter.WriteAsync($$"""
                 {
                        return await transport.callMethod({{path}}, {{parameters}}).ConfigureAwait(false);
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
        await using StreamWriter enumsWriter = openFileStream("Enums.cs");

        await enumsWriter.WriteAsync(FILE_HEADER);
        await enumsWriter.WriteAsync($"namespace {NAMESPACE};");

        foreach (DocXConfiguration command in documentation.commands.Concat(documentation.configurations)) {
            await enumsWriter.WriteAsync(string.Join(null, command.parameters.Where(parameter => parameter.type == DataType.ENUM).Cast<EnumParameter>().Select(parameter => {
                string enumTypeName = getEnumName(command, parameter.name);

                string xapiNameToCsName(EnumValue value) {
                    bool   isTimeZone = command.name.SequenceEqual(new[] { "xConfiguration", "Time", "Zone" }) && parameter.name == "Zone";
                    string name       = isTimeZone ? value.name : string.Join(null, value.name.Split('-').Select(s => s.ToUpperFirstLetter()));
                    name = Regex.Replace(name, @"[^a-z0-9_]", match => match.Value switch {
                        "."                                               => "_",
                        "/"                                               => "Ⳇ",      //"Ⳇ",
                        "+" when isTimeZone                               => "_Plus_", //ႵᏐǂߙƚϯᵻᵼ
                        "-" when isTimeZone && value.name.Contains("GMT") => "_Minus_",
                        "-" when isTimeZone                               => "_",
                        _                                                 => string.Empty
                    }, RegexOptions.IgnoreCase).ToUpperFirstLetter();
                    return char.IsLetter(name[0]) ? name : "_" + name;
                }

                IEnumerable<string> switchArms = parameter.possibleValues
                    .Where(value => xapiNameToCsName(value) != value.name)
                    .Select(value => $"{enumTypeName}.{xapiNameToCsName(value)} => \"{value.name}\"")
                    .ToList();

                string toStringBody = switchArms.Any()
                    ? $$"""
                        enumValue switch {
                                {{string.Join(",\r\n        ", switchArms.Append("_ => enumValue.ToString()"))}}
                            }
                        """
                    : "enumValue.ToString()";

                return $$"""
                
                
                public enum {{enumTypeName}} {

                {{string.Join(",\r\n\r\n", parameter.possibleValues.Select(value => $"    /// <summary><c>{value.name}</c></summary>\r\n    {xapiNameToCsName(value)}"))}}

                }

                public static class {{enumTypeName}}Extensions {

                    public static string ToXapiString(this {{enumTypeName}} enumValue) => {{toStringBody}};

                }

                """;
            })));
        }
    }

    private static async Task writeConfiguration(ExtractedDocumentation documentation) {
        await using StreamWriter configurationWriter  = openFileStream("Configuration.cs");
        await using StreamWriter iconfigurationWriter = openFileStream("IConfiguration.cs");

        foreach (DocXConfiguration configuration in documentation.configurations) {
            configuration.name = configuration.name.Where((s, i) =>
                    !configuration?.parameters.Any(parameter => parameter is IntParameter { arrayIndexItemParameterPosition: { } paramIndex } && paramIndex == i) ?? true)
                .ToList();
        }

        IDictionary<string, ISet<InterfaceChild>> interfaceTree = generateInterfaceTree(documentation.configurations);

        await iconfigurationWriter.WriteAsync(FILE_HEADER);
        await iconfigurationWriter.WriteAsync($"""
            using System.Xml.Linq;
            
            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await iconfigurationWriter.WriteAsync($"public interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod { command: DocXConfiguration configuration } when configuration.parameters.Any():
                        await iconfigurationWriter.WriteAsync($"""
                /// <summary>
                /// {configuration.description.NewLinesToParagraphs()}
                /// </summary>
            {string.Join("\r\n", configuration.parameters.Select(param => $"    /// <param name=\"{getParameterName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                /// <returns>A <see cref="Task"/> that will complete asynchronously when the configuration change has been received by the device.</returns>
                {generateMethodSignature(configuration, true, true)};

                /// <summary>
                /// {configuration.description.NewLinesToParagraphs()}
                /// </summary>
                /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>
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
    }

    private static IDictionary<string, ISet<InterfaceChild>> generateInterfaceTree(IEnumerable<AbstractCommand> documentation) {
        IDictionary<string, ISet<InterfaceChild>> interfaceTree = new Dictionary<string, ISet<InterfaceChild>>();

        foreach (AbstractCommand command in documentation) {
            string childInterfaceName = getInterfaceName(command);
            putInterfaceChild(childInterfaceName, new InterfaceMethod(command));

            for (int length = command.name.Count - 1; length > 1; length--) {
                string parentInterfaceName = getInterfaceName(new DocXStatus { name = command.name.Take(length).ToList() });
                putInterfaceChild(parentInterfaceName, new Subinterface(childInterfaceName, command.name[length - 1]));
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
        $"{(isInterfaceMethod ? string.Empty : "async ")}Task<XElement> {(isInterfaceMethod ? string.Empty : getInterfaceName(command) + '.')}{command.name.Last()}({string.Join(", ", command.parameters.OrderByDescending(parameter => parameter.required).Select(parameter => $"{parameter.type switch {
            DataType.INTEGER => "int",
            DataType.STRING  => "string",
            DataType.ENUM    => getEnumName(command, parameter.name)
        }}{(parameter.required ? string.Empty : "?")} {getParameterName(parameter)}{(parameter.required || !isInterfaceMethod ? string.Empty : " = null")}"))})";

    private static string generateMethodSignature(DocXConfiguration configuration, bool isSetter, bool isInterfaceMethod) =>
        $"{(isInterfaceMethod ? string.Empty : "async ")}Task{(isSetter ? "" : "<XElement>")} {(isInterfaceMethod ? string.Empty : getInterfaceName(configuration) + '.')}{configuration.name.Last()}({string.Join(", ", configuration.parameters.SkipLast(isSetter ? 0 : 1).Select(parameter => $"{parameter.type switch {
            DataType.INTEGER => "int",
            DataType.STRING  => "string",
            DataType.ENUM    => getEnumName(configuration, parameter.name)
        }} {getParameterName(parameter)}"))})";

    private static string getInterfaceName(AbstractCommand method) {
        return "I" + string.Join(null,
            method.name.Skip(1)
                .SkipLast(1)
                .Append(method.name[0][1..] + 's')); //will need to be "es" for Statuses
    }

    private static string getEnumName(AbstractCommand command, string parameterName) {
        return string.Join(null, command.name.Skip(1).Append(parameterName));
    }

    private static string getParameterName(Parameter param, bool forDocumentationXmlComment = false) {
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