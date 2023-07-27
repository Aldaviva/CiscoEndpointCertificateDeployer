using CSxAPI.API;
using StreamJsonRpc;

namespace CSxAPI;

public interface ICSxAPIClient: IDisposable, IAsyncDisposable {

    string Hostname { get; }
    string Username { get; }
    bool IsConnected { get; }
    bool AllowSelfSignedTls { get; set; }
    bool ConsoleTracing { get; set; }

    ICommands Command { get; }
    IConfigurations Configuration { get; }
    IStatuses Status { get; }
    IEvents Event { get; }

    event EventHandler<JsonRpcDisconnectedEventArgs>? Disconnected;

    Task<ICSxAPIClient> Connect(CancellationToken cancellationToken = default);

}