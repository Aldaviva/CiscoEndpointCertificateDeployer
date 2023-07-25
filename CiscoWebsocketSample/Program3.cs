using CiscoWebsocketXapi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CiscoWebsocketSample;

public class Program3 {

    public class MyTestClass {

        public string? a { get; init; }

        public MyTestClass() {
            Console.WriteLine($"In the constructor, A is {a ?? "null"}");
        }

    }

    public static async Task Main() {
        using WebSocketXApi jsonRpcXapi = new JsonRpcXapi("whisperblade.aldaviva.com", "ben", Environment.GetEnvironmentVariable("password") ?? "", true) { ConsoleTracing = true };
        return;
        await jsonRpcXapi.connect();

        JsonWriter jsonWriter     = new JsonTextWriter(Console.Out) { Formatting = Formatting.Indented };
        long       subscriptionId1 = await jsonRpcXapi.Subscribe(new[] { "Event", "Audio" }, json => json.WriteTo(jsonWriter));

        // await jsonRpcXapi.Command(new[] { "Audio", "VuMeter", "Start" }, new { ConnectorId = 1, ConnectorType = "Microphone", IntervalMs = 1000, Source = "BeforeAEC" });
        // await jsonRpcXapi.Command(new[] { "Audio", "VuMeter", "Start" }, new { ConnectorId = 2, ConnectorType = "Microphone", IntervalMs = 1000, Source = "BeforeAEC" });

        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, eventArgs) => {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine("Press Ctrl+C to exit");
        cts.Token.WaitHandle.WaitOne();

        await jsonRpcXapi.Unsubscribe(subscriptionId1);
        // await jsonRpcXapi.Command(new[] { "Audio", "VuMeter", "StopAll" });
    }

}