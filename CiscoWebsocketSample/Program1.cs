using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CiscoWebsocketSample;

public class Program1 {

    public static async Task Main(string[] args) {

        JsonSerializerOptions jsonOptions = new() { Converters = { new ObjectToInferredTypesConverter() } };

        using ClientWebSocket webSocket = new();
        webSocket.Options.SetRequestHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("ben:" + Environment.GetEnvironmentVariable("password"))));
        webSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true; // allow self-signed certificates
// webSocket.Options.Proxy                               = new WebProxy("127.0.0.1", 9998);
        // webSocket.Options.UseDefaultCredentials = true;

        await webSocket.ConnectAsync(new Uri("wss://roomkit.aldaviva.com/ws"), CancellationToken.None);

        Request request      = new(103, "xGet", new Dictionary<string, object> { { "Path", new List<string> { "Status", "SystemUnit", "State" } } });
        byte[]  requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, jsonOptions);
        await webSocket.SendAsync(new ReadOnlyMemory<byte>(requestBytes), WebSocketMessageType.Text, true, CancellationToken.None);

        var                         receiveBuffer = new Memory<byte>(new byte[1024]);
        ValueWebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(receiveBuffer, CancellationToken.None);
        Response?                   response      = JsonSerializer.Deserialize<Response>(receiveBuffer[..receiveResult.Count].Span, jsonOptions);

        long activeCallCount = (long) response!.result["NumberOfActiveCalls"];
        Console.WriteLine($"System has {activeCallCount:N0} active calls.");

    }

}

public class Request {

    [JsonPropertyName("jsonrpc")]
    public string version { get; } = "2.0";

    public long id { get; set; }
    public string method { get; set; }

    [JsonPropertyName("params")]
    public IDictionary<string, object> parameters { get; }

    public Request(long id, string method, IDictionary<string, object>? parameters = null) {
        this.id         = id;
        this.method     = method;
        this.parameters = parameters ?? new Dictionary<string, object>();
    }

}

public class Response {

    [JsonPropertyName("jsonrpc")]
    public string version { get; } = "2.0";

    public long id { get; set; }
    public IDictionary<string, object> result { get; set; } = null!;

}

/// <summary>
/// When the C# type to deserialize is <c>object</c> (or <c>IDictionary&lt;string, object&gt;</c>), convert the JSON values into their corresponding CLR types like <c>long</c> and <c>string</c> instead of <c>JsonElement</c> so the caller doesn't have to be aware of JSON parsing or recursion.
/// </summary>
/// <remarks>Source: https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to#deserialize-inferred-types-to-object-properties</remarks>
public class ObjectToInferredTypesConverter: JsonConverter<object> {

    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch {
        JsonTokenType.True                                       => true,
        JsonTokenType.False                                      => false,
        JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
        JsonTokenType.Number                                     => reader.GetDouble(),
        JsonTokenType.String                                     => reader.GetString()!,
        _                                                        => JsonDocument.ParseValue(ref reader).RootElement.Clone()
    };

    public override void Write(Utf8JsonWriter writer, object objectToWrite, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);

}