using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public class CsClientWriter {

    private const string GENERATED_DIR = @"..\..\..\..\csxapi\Generated";

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
        await using FileStream commandsFile  = new(Path.Combine(GENERATED_DIR, "Commands.cs"), FileMode.Create, FileAccess.Write, FileShare.Read);
        await using FileStream icommandsFile = new(Path.Combine(GENERATED_DIR, "ICommands.cs"), FileMode.Create, FileAccess.Write, FileShare.Read);
        await using FileStream enumsFile     = new(Path.Combine(GENERATED_DIR, "Enums.cs"), FileMode.Create, FileAccess.Write, FileShare.Read);

        await using StreamWriter commandsWriter  = new(commandsFile, UTF8);
        await using StreamWriter icommandsWriter = new(icommandsFile, UTF8);
        await using StreamWriter enumsWriter     = new(enumsFile, UTF8);

        IDictionary<string, ISet<InterfaceChild>> interfaceTree = new Dictionary<string, ISet<InterfaceChild>>();

        await enumsWriter.WriteAsync(FILE_HEADER);
        await enumsWriter.WriteAsync("namespace csxapi;");

        foreach (DocXCommand command in documentation.commands) {
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

        await icommandsWriter.WriteAsync(FILE_HEADER);
        await icommandsWriter.WriteAsync("""
            using System.Xml.Linq;
            
            namespace csxapi;


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await icommandsWriter.WriteAsync($"public interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod { command: DocXCommand command } method:
                        await icommandsWriter.WriteAsync($"""
                /// <summary>
                /// {method.command.description.NewLinesToParagraphs()}
                /// </summary>
            {string.Join("\r\n", command.parameters.Select(param => $"    /// <param name=\"{getParameterName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the XML response from the device.</returns>
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

            namespace csxapi;

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

            await enumsWriter.WriteAsync(string.Join(null, command.parameters.Where(parameter => parameter.type == DataType.ENUM).Cast<EnumParameter>().Select(parameter => {
                string enumTypeName = getEnumName(command, parameter.name);

                static string xapiNameToCsName(EnumValue value) {
                    string name = string.Join(null, value.name.Split('-').Select(s => s.ToUpperFirstLetter()));
                    name = Regex.Replace(name, @"[^a-z0-9_]", match => match.Value == "." ? "_" : string.Empty, RegexOptions.IgnoreCase).ToUpperFirstLetter();
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

                    {{string.Join(", ", parameter.possibleValues.Select(xapiNameToCsName))}}

                }

                public static class {{enumTypeName}}Extensions {

                    public static string ToXapiString(this {{enumTypeName}} enumValue) => {{toStringBody}};

                }

                """;
            })));
        }

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface subinterface in interfaceNode.Value.OfType<Subinterface>()) {
                await commandsWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await commandsWriter.WriteAsync("}");

    }

    private static string generateMethodSignature(DocXCommand command, bool isInterfaceMethod) =>
        $"{(isInterfaceMethod ? string.Empty : "async ")}Task<XElement> {(isInterfaceMethod ? string.Empty : getInterfaceName(command) + '.')}{command.name.Last()}({string.Join(", ", command.parameters.OrderByDescending(parameter => parameter.required).Select(parameter => $"{parameter.type switch {
            DataType.INTEGER => "int",
            DataType.STRING  => "string",
            DataType.ENUM    => getEnumName(command, parameter.name)
        }}{(parameter.required ? string.Empty : "?")} {getParameterName(parameter)}{(parameter.required || !isInterfaceMethod ? string.Empty : " = null")}"))})";

    private static string getInterfaceName(AbstractCommand method) {
        return "I" + string.Join(null, method.name.Skip(1).SkipLast(1).Append(method.name[0][1..] + 's'));
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