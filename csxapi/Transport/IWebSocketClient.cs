using StreamJsonRpc;

namespace CSxAPI.Transport;

public interface IWebSocketClient {

    bool ConsoleTracing { get; set; }

    bool AllowSelfSignedTls { get; set; }

    Task Connect(CancellationToken? cancellationToken = null);

    bool IsConnected { get; }

    event EventHandler<JsonRpcDisconnectedEventArgs>? Disconnected;

}