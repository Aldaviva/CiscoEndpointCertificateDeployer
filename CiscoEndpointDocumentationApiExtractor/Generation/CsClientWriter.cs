using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public partial class CsClientWriter {

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
        Console.WriteLine("Generating xConfiguration client");
        await writeConfiguration(documentation);

        Console.WriteLine("Generating xCommand client");
        await writeCommands(documentation);

        Console.WriteLine("Generating xStatus client");
        await writeStatuses(documentation);

        Console.WriteLine("Generating xEvent client");
        await writeEvents(documentation);

        Console.WriteLine("Generating enums");
        await writeEnums(documentation);
    }

    private static StreamWriter openFileStream(string filename) {
        string filePath = Path.Combine(GENERATED_DIR, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        return new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read), UTF8);
    }

    private static IDictionary<string, ISet<InterfaceChild<T>>> generateInterfaceTree<T>(IEnumerable<T> documentation) where T: IPathNamed {
        IDictionary<string, ISet<InterfaceChild<T>>> interfaceTree = new Dictionary<string, ISet<InterfaceChild<T>>>();

        foreach (T command in documentation) {
            string childInterfaceName = getInterfaceName(command);
            putInterfaceChild(childInterfaceName, new InterfaceMethod<T>(command));

            for (int length = command.nameWithoutBrackets.Count - 1; length > 1; length--) {
                string parentInterfaceName = getInterfaceName(new DocXStatus { name = command.nameWithoutBrackets.Take(length).ToList() });
                putInterfaceChild(parentInterfaceName, new Subinterface<T>(childInterfaceName, command.nameWithoutBrackets[length - 1]));
                childInterfaceName = parentInterfaceName;
            }

            void putInterfaceChild(string interfaceName, InterfaceChild<T> child) {
                if (interfaceTree.TryGetValue(interfaceName, out ISet<InterfaceChild<T>>? entry)) {
                    entry.Add(child);
                } else {
                    interfaceTree.Add(interfaceName, new HashSet<InterfaceChild<T>> { child });
                }
            }
        }

        return interfaceTree;
    }

    private static string getInterfaceName(IPathNamed method) {
        string singular = "I" + string.Join(null, method.nameWithoutBrackets.Skip(1)
            .SkipLast(1)
            .Append(method.nameWithoutBrackets[0][1..]));
        return singular + (singular.EndsWith('s') ? "es" : "s");
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