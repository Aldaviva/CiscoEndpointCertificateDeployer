using CiscoWebsocketXapi;

namespace CiscoWebsocketSample;

public class Program3 {

    public static async Task Main(string[] args) {
        using WebSocketXApi jsonRpcXapi = new JsonRpcXapi("whisperblade.aldaviva.com", "ben", Environment.GetEnvironmentVariable("password") ?? "") { ConsoleTracing = true };
        await jsonRpcXapi.connect();

        string defaultCallProtocol = await jsonRpcXapi.Get<string>("Configuration", "Conference", "DefaultCall", "Protocol");

        Console.WriteLine(defaultCallProtocol);
    }

}