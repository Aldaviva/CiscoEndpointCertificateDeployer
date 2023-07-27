using System.Net;
using CSxAPI.API;
using CSxAPI.Transport;
using StreamJsonRpc;

namespace CSxAPI;

public class CSxAPIClient: ICSxAPIClient {

    public string Hostname { get; }
    public string Username => _credentials.UserName;
    public bool IsConnected => _transport.IsConnected;

    public bool AllowSelfSignedTls {
        get => _transport.AllowSelfSignedTls;
        set => _transport.AllowSelfSignedTls = value;
    }

    public bool ConsoleTracing {
        get => _transport.ConsoleTracing;
        set => _transport.ConsoleTracing = value;
    }

    public ICommands Command { get; }
    public IConfigurations Configuration { get; }
    public IStatuses Status { get; }
    public IEvents Event { get; }

    public event EventHandler<JsonRpcDisconnectedEventArgs>? Disconnected {
        add => _transport.Disconnected += value;
        remove => _transport.Disconnected -= value;
    }

    private readonly NetworkCredential _credentials;
    private readonly IWebSocketXapi    _transport;

    public CSxAPIClient(string hostname, string username, string password): this(hostname, new NetworkCredential(username, password)) { }

    public CSxAPIClient(string hostname, NetworkCredential credentials) {
        Hostname     = hostname;
        _credentials = credentials;
        _transport   = new WebSocketXapi(Hostname, _credentials);

        FeedbackSubscriber feedbackSubscriber = new(_transport);

        Command       = new Commands(_transport);
        Configuration = new Configurations(_transport, feedbackSubscriber);
        Status        = new Statuses(_transport, feedbackSubscriber);
        Event         = new Events(feedbackSubscriber);
    }

    public async Task<ICSxAPIClient> Connect(CancellationToken cancellationToken = default) {
        await _transport.Connect(cancellationToken).ConfigureAwait(false);
        return this;
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            _transport.Dispose();
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync() {
        GC.SuppressFinalize(this);
        return _transport.DisposeAsync();
    }

}