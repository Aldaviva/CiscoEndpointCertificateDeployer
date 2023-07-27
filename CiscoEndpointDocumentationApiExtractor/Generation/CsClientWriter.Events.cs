using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public static partial class CsClientWriter {

    private static async Task writeEvents(ExtractedDocumentation documentation) {
        await using StreamWriter eventWriter             = openFileStream("Events.cs");
        await using StreamWriter ieventWriter            = openFileStream("IEvents.cs");
        await using StreamWriter eventDataWriter         = openFileStream("Data\\Events.cs");
        await using StreamWriter eventDeserializerWriter = openFileStream("Serialization\\EventDeserializer.cs");

        IDictionary<string, ISet<InterfaceChild<DocXEvent>>> interfaceTree = generateInterfaceTree(documentation.events);

        await ieventWriter.WriteAsync($"""
            {FILE_HEADER}

            using {NAMESPACE}.Data;
            using System.CodeDom.Compiler;

            namespace {NAMESPACE};


            """);

        foreach (KeyValuePair<string, ISet<InterfaceChild<DocXEvent>>> interfaceNode in interfaceTree) {
            await ieventWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild<DocXEvent> interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod<DocXEvent> { command: var xEvent }:
                        await ieventWriter.WriteAsync($"""
                                /// <summary>
                                /// <para><c>{string.Join(' ', xEvent.name)}</c></para>
                                /// <para>Fired when the event is received from the device.</para>
                                /// </summary>
                                {generateEventSignature(xEvent, true).signature};


                            """);
                        break;

                    case Subinterface<DocXEvent> s:
                        await ieventWriter.WriteAsync($"    {s.interfaceName} {s.getterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await ieventWriter.WriteAsync("}\r\n\r\n");
        }

        await eventDataWriter.WriteAsync($"""
            {FILE_HEADER}

            using System.CodeDom.Compiler;

            namespace {NAMESPACE}.Data;


            """);

        await eventDeserializerWriter.WriteAsync($$"""
            {{FILE_HEADER}}

            using {{NAMESPACE}}.Data;
            using Newtonsoft.Json.Linq;
            using System.CodeDom.Compiler;
            using System.Collections.ObjectModel;

            namespace {{NAMESPACE}}.Serialization;

            {{GENERATED_ATTRIBUTE}}
            internal static class EventDeserializer {
                
                private static readonly IDictionary<Type, Func<JToken, object>> eventDeserializers = new Dictionary<Type, Func<JToken, object>>();

                public static T Deserialize<T>(JObject serialized) => (T) eventDeserializers[typeof(T)](serialized);

                static EventDeserializer() {
            
            """);

        Stack<IEventParent> eventClassesToGenerate = new(documentation.events.Where(xEvent => xEvent.children.Any()));

        while (eventClassesToGenerate.TryPop(out IEventParent? eventClassToGenerate)) {
            await writeEventDataClass(eventClassToGenerate);
            await writeEventDeserializer(eventClassToGenerate);
        }

        await eventDeserializerWriter.WriteAsync("    }\r\n\r\n}");

        async Task writeEventDataClass(IEventParent eventParent) {
            await eventDataWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic class {generateEventDataClassName(eventParent)} {{\r\n\r\n");

            foreach (EventChild eventChild in eventParent.children) {
                switch (eventChild) {
                    case ValueChild valueChild: {
                        string childType = valueChild.type switch {
                            DataType.INTEGER => "int",
                            DataType.STRING  => "string",
                            DataType.ENUM    => getEnumName(valueChild.name)
                        };
                        await eventDataWriter.WriteAsync(
                            $"    public {childType}{(valueChild.required ? "" : "?")} {xapiEventKeyToCsIdentifier(valueChild.name.Last())} {{ get; init; }}{(valueChild is { required: true, type: DataType.STRING } ? " = null!;" : "")}\r\n");
                        break;
                    }
                    case ListContainer listChild: {
                        string childType = generateEventDataClassName(listChild);
                        await eventDataWriter.WriteAsync($"    public IDictionary<int, {childType}> {listChild.name.Last()} {{ get; init; }} = null!;\r\n");
                        eventClassesToGenerate.Push(listChild);
                        break;
                    }
                    case ObjectContainer objectContainer: {
                        string childType = generateEventDataClassName(objectContainer);
                        await eventDataWriter.WriteAsync($"    public {childType}{(objectContainer.required ? "" : "?")} {objectContainer.name.Last()} {{ get; init; }}{(objectContainer.required ? " = null!;" : "")}\r\n");
                        eventClassesToGenerate.Push(objectContainer);
                        break;
                    }
                }
            }

            await eventDataWriter.WriteAsync("\r\n}\r\n\r\n");
        }

        async Task writeEventDeserializer(IEventParent eventParent) {
            string typeName = generateEventDataClassName(eventParent);

            int serializedParameterCounter = 0;
            await eventDeserializerWriter.WriteAsync($$"""
                        eventDeserializers.Add(typeof({{typeName}}), json => new {{typeName}} {
                            {{string.Join(",\r\n            ", eventParent.children.Select(child => {
                                string lastChildName = child.name.Last();
                                string childClass    = generateEventDataClassName(child);
                                return xapiEventKeyToCsIdentifier(lastChildName) + " = " + child switch {
                                    IntChild { implicitAnonymousSingleton: true } => "json.Value<int>()",
                                    IntChild { required: true } => $"json.Value<int>(\"{lastChildName}\")",
                                    IntChild { required: false } => $"json.Value<int?>(\"{lastChildName}\")",
                                    StringChild { required: true } => $"json.Value<string>(\"{lastChildName}\")!",
                                    StringChild { required: false } => $"json.Value<string>(\"{lastChildName}\")",
                                    EnumChild { required: true } => $"EnumSerializer.Deserialize<{getEnumName(child.name)}>(json.Value<string>(\"{lastChildName}\")!)",
                                    EnumChild { required: false } => $"json.Value<string>(\"{lastChildName}\") is {{ }} serialized{++serializedParameterCounter} ? EnumSerializer.Deserialize<{getEnumName(child.name)}>(serialized{serializedParameterCounter}) : null",
                                    ListContainer => $"(IDictionary<int, {childClass}>?) json[\"{lastChildName}\"]?.Children().ToDictionary(innerObject => innerObject.Value<int>(\"id\"), innerObject => Deserialize<{childClass}>((JObject) innerObject)) ?? new ReadOnlyDictionary<int, {childClass}>(new Dictionary<int, {childClass}>(0))",
                                    ObjectContainer { required: true } => $"Deserialize<{childClass}>((JObject) json[\"{lastChildName}\"]!)",
                                    ObjectContainer { required: false } => $"json[\"{lastChildName}\"] is JObject serialized{++serializedParameterCounter} ? Deserialize<{childClass}>(serialized{serializedParameterCounter}) : null"
                                };
                            }))}}
                        });


                """);
        }

        await eventWriter.WriteAsync($$"""
            {{FILE_HEADER}}

            using {{NAMESPACE}}.Data;
            using {{NAMESPACE}}.Serialization;
            using {{NAMESPACE}}.Transport;
            using System.CodeDom.Compiler;

            namespace {{NAMESPACE}};

            {{GENERATED_ATTRIBUTE}}
            internal class Events: {{string.Join(", ", interfaceTree.Keys)}} {

                private readonly IXapiTransport transport;
                private readonly FeedbackSubscriber feedbackSubscriber;


            """);

        foreach (DocXEvent xEvent in documentation.events) {
            (string eventSignature, string? returnType) = generateEventSignature(xEvent, false);
            await eventWriter.WriteAsync($$"""
                    /// <inheritdoc />
                    {{eventSignature}} {
                        add => feedbackSubscriber.Subscribe(new[] { {{string.Join(", ", xEvent.name.Select(s => $"\"{s}\""))}} }, value{{(returnType != null ? $", ValueSerializer.DeserializeEvent<{returnType}>" : "")}}).Wait();
                        remove => feedbackSubscriber.Unsubscribe(value).Wait();
                    }


                """
            );
        }

        foreach (KeyValuePair<string, ISet<InterfaceChild<DocXEvent>>> interfaceNode in interfaceTree) {
            foreach (Subinterface<DocXEvent> subinterface in interfaceNode.Value.OfType<Subinterface<DocXEvent>>()) {
                await eventWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await eventWriter.WriteAsync("}");
    }

    private static (string signature, string? returnType) generateEventSignature(DocXEvent xEvent, bool isInterfaceEvent) {
        string? payloadType = xEvent.children.Any() ? generateEventDataClassName(xEvent) : null;
        return ($"event FeedbackCallback{(payloadType != null ? $"<{payloadType}>" : "")} {(isInterfaceEvent ? "" : getInterfaceName(xEvent) + '.')}{xEvent.nameWithoutBrackets.Last()}", payloadType);
    }

    private static string generateEventDataClassName(IPathNamed c) {
        return string.Join(null, c.name.Skip(1).Append(c.name[0][1..]).Append("Data"));
    }

    private static string xapiEventKeyToCsIdentifier(string key) => key.Replace('.', '_');

}