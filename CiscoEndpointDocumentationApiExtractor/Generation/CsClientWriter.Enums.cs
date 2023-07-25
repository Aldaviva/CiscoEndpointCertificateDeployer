using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public partial class CsClientWriter {

    private static async Task writeEnums(ExtractedDocumentation documentation) {
        await using StreamWriter enumsWriter = openFileStream("Data\\Enums.cs");
        await using StreamWriter enumSerializerWriter = openFileStream("Serialization\\EnumSerializer.cs");

        await enumsWriter.WriteAsync($"""
            {FILE_HEADER}

            using System.CodeDom.Compiler;

            namespace {NAMESPACE}.Data;


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

        foreach (DocXEvent xEvent in documentation.events) {
            await writeEventParentEnum(xEvent);
        }

        async Task writeEventParentEnum(IEventParent eventParent) {
            foreach (EventChild eventChild in eventParent.children) {
                if (eventChild is EnumChild enumChild) {
                    string enumTypeName = getEnumName(enumChild.name);

                    await enumsWriter.WriteAsync($$"""
                        public enum {{enumTypeName}} {

                        {{string.Join(",\r\n\r\n", enumChild.possibleValues.Select(value => $"    /// <summary><para><c>{value.name}</c></para></summary>\r\n    {xapiEnumValueToCsIdentifier(null, null, value)}"))}}

                        }


                        """);
                } else if (eventChild is IEventParent subParent) {
                    await writeEventParentEnum(subParent);
                }
            }
        }

        await enumSerializerWriter.WriteAsync($$"""
            {{FILE_HEADER}}
            
            using {{NAMESPACE}}.Data;
            using System.CodeDom.Compiler;
            
            namespace {{NAMESPACE}}.Serialization;

            {{GENERATED_ATTRIBUTE}}
            internal static class EnumSerializer {

                private static readonly IDictionary<Type, (Func<Enum, string> serialize, Func<string, Enum> deserialize)> enumSerializers = new Dictionary<Type, (Func<Enum, string>, Func<string, Enum>)>();

                public static T Deserialize<T>(string serialized) where T: Enum => (T) enumSerializers[typeof(T)].deserialize(serialized);

                public static string Serialize<T>(T deserialized) where T: Enum => enumSerializers[typeof(T)].serialize(deserialized);

                private static string DefaultSerializer(Enum o) => o.ToString();

                static EnumSerializer() {

            """);

        foreach (DocXConfiguration command in documentation.commands.Concat(documentation.configurations)) {
            await enumSerializerWriter.WriteAsync(string.Join(null, command.parameters.Where(parameter => parameter.type == DataType.ENUM).Cast<EnumParameter>().Select(parameter => {
                string enumTypeName = getEnumName(command, parameter.name);

                IEnumerable<string> serializerSwitchArms = parameter.possibleValues
                    // .Where(value => xapiEnumValueToCsIdentifier(command, parameter, value) != value.name)
                    .Select(value => $"{enumTypeName}.{xapiEnumValueToCsIdentifier(command, parameter, value)} => \"{value.name}\"")
                    .ToList();

                IEnumerable<string> deserializerSwitchArms =
                    parameter.possibleValues.Select(value => $"\"{value.name}\" => {enumTypeName}.{xapiEnumValueToCsIdentifier(command, parameter, value)}");

                return $$"""
                            enumSerializers.Add(typeof({{enumTypeName}}), (
                                serialize: deserialized => ({{enumTypeName}}) deserialized switch {
                                    {{string.Join(",\r\n                ", serializerSwitchArms.Append("_ => deserialized.ToString()"))}}
                                },
                                deserialize: serialized => serialized switch {
                                    {{string.Join(",\r\n                ", deserializerSwitchArms.Append($"_ => throw new ArgumentOutOfRangeException(nameof(serialized), serialized, $\"Unknown {enumTypeName} enum value {{serialized}}, known values are {{string.Join(\", \", Enum.GetValues<{enumTypeName}>())}}.\")"))}}
                                }));


                    """;

            })));
        }

        foreach (DocXStatus xStatus in documentation.statuses.Where(status => status.returnValueSpace.type == DataType.ENUM)) {
            EnumValueSpace valueSpace   = (EnumValueSpace) xStatus.returnValueSpace;
            string         enumTypeName = getEnumName(xStatus);

            IEnumerable<string> deserializerSwitchArms =
                valueSpace.possibleValues.Select(value => $"\"{value.name}\" => {enumTypeName}.{xapiEnumValueToCsIdentifier(xStatus, null, value)}");

            await enumSerializerWriter.WriteAsync($$"""
                        enumSerializers.Add(typeof({{enumTypeName}}), (
                            serialize: DefaultSerializer,
                            deserialize: serialized => serialized switch {
                                {{string.Join(",\r\n                ", deserializerSwitchArms.Append($"_ => throw new ArgumentOutOfRangeException(nameof(serialized), serialized, $\"Unknown {enumTypeName} enum value {{serialized}}, known values are {{string.Join(\", \", Enum.GetValues<{enumTypeName}>())}}.\")"))}}
                            }));


                """);
        }

        foreach (DocXEvent xEvent in documentation.events) {
            await writeEventSerializer(xEvent);
        }

        async Task writeEventSerializer(IEventParent eventParent) {
            foreach (EventChild eventChild in eventParent.children) {
                if (eventChild is EnumChild enumChild) {
                    string enumTypeName = getEnumName(enumChild.name);
                    IEnumerable<string> deserializerSwitchArms =
                        enumChild.possibleValues.Select(value => $"\"{value.name}\" => {enumTypeName}.{xapiEnumValueToCsIdentifier(null, null, value)}");

                    await enumSerializerWriter.WriteAsync($$"""
                                enumSerializers.Add(typeof({{enumTypeName}}), (
                                    serialize: DefaultSerializer,
                                    deserialize: serialized => serialized switch {
                                        {{string.Join(",\r\n                ", deserializerSwitchArms.Append($"_ => throw new ArgumentOutOfRangeException(nameof(serialized), serialized, $\"Unknown {enumTypeName} enum value {{serialized}}, known values are {{string.Join(\", \", Enum.GetValues<{enumTypeName}>())}}.\")"))}}
                                    }));


                        """);
                } else if (eventChild is IEventParent subParent) {
                    await writeEventSerializer(subParent);
                }
            }
        }

        await enumSerializerWriter.WriteAsync("    }\r\n}");

        static string xapiEnumValueToCsIdentifier(AbstractCommand? command, EnumParameter? parameter, EnumValue value) {
            bool   isTimeZone = (command?.name.SequenceEqual(new[] { "xConfiguration", "Time", "Zone" }) ?? false) && parameter?.name == "Zone";
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

    private static string getEnumName(AbstractCommand command, string? parameterName) {
        IEnumerable<string> segments = command.nameWithoutBrackets;
        if (parameterName != null) {
            segments = segments.Append(parameterName);
        }

        return string.Join(null, segments.DistinctConsecutive()).TrimStart('x');
    }
    
    private static string getEnumName(DocXStatus command) {
        return getEnumName(command, null);
    }
    
    private static string getEnumName(DocXConfiguration command, string parameterName) {
        return getEnumName((AbstractCommand) command, parameterName);
    }

    private static string getEnumName(ICollection<string> eventParameterName) {
        return string.Join(null, eventParameterName.DistinctConsecutive()).TrimStart('x');
    }

}