using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace CSxAPI.Serialization;

internal static class ValueSerializer {

    public static string Deserialize(string serialized) => serialized;

    public static int Deserialize(int serialized) => serialized;

    public static int? Deserialize(string serialized, string optionalValue) => serialized == optionalValue ? null : int.Parse(serialized);

    public static T Deserialize<T>(string serialized) where T: Enum => EnumSerializer.Deserialize<T>(serialized);

    public static string Serialize(string deserialized) => deserialized;

    public static int Serialize(int deserialized) => deserialized;

    public static string Serialize(int? deserialized, string optionalValue) => deserialized?.ToString() ?? optionalValue;

    [return: NotNullIfNotNull(nameof(deserialized))]
    public static string? Serialize(Enum? deserialized) => deserialized is null ? null : EnumSerializer.Serialize(deserialized);

    public static T DeserializeEvent<T>(JObject serialized) => EventDeserializer.Deserialize<T>(serialized);

}