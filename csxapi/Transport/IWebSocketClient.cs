namespace CSxAPI.Transport;

public interface IWebSocketClient {

    bool ConsoleTracing { get; set; }

    bool AllowSelfSignedTls { get; init; }

    Task Connect(CancellationToken? cancellationToken = null);

}