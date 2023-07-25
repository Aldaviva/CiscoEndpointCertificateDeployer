using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace CSxAPI;

[SuppressMessage("ReSharper", "CoVariantArrayConversion")]
public class WebSocketXapi: IXapiTransport {

    public string Hostname { get; }
    public string Username { get; }

    private readonly ClientWebSocket                    _webSocket = new();
    private readonly JsonRpc                            _jsonRpc;
    private readonly TraceListener                      _consoleTraceListener = new ConsoleTraceListener();
    private readonly IDictionary<long, Action<JObject>> _feedbackCallbacks    = new Dictionary<long, Action<JObject>>();

    public bool ConsoleTracing {
        get => _jsonRpc.TraceSource.Switch.Level == SourceLevels.All;
        set {
            TraceSource traceSource = _jsonRpc.TraceSource;

            traceSource.Switch.Level = value ? SourceLevels.All : SourceLevels.Warning | SourceLevels.ActivityTracing;
            if (value && !traceSource.Listeners.Contains(_consoleTraceListener)) {
                traceSource.Listeners.Add(_consoleTraceListener);
            } else {
                traceSource.Listeners.Remove(_consoleTraceListener);
            }
        }
    }

    public WebSocketXapi(string hostname, string username, string password, bool allowSelfSigned = false) {
        Hostname = hostname;
        Username = username;

        _webSocket.Options.SetRequestHeader("Authorization", "Basic " + Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(username + ":" + password), Base64FormattingOptions.None));

        if (allowSelfSigned) {
            _webSocket.Options.RemoteCertificateValidationCallback = delegate { return true; };
        }

        _jsonRpc = new JsonRpc(new WebSocketMessageHandler(_webSocket));
    }

    public async Task connect(CancellationToken? cancellationToken = null) {
        Uri uri = new UriBuilder("wss", Hostname, -1, "ws").Uri;
        await _webSocket.ConnectAsync(uri, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
        _jsonRpc.AddLocalRpcMethod("xFeedback/Event", onFeedbackEvent);
        _jsonRpc.StartListening();
    }

    /**
     * StreamJsonRpc insists on matching parameter names, so provide lots of optional parameters with all possible names, and just pick the one that isn't null
     */
    [SuppressMessage("ReSharper", "InconsistentNaming")] // parameter names must exactly match what the endpoint sends in JSON, case sensitively, or this method won't be called
    private void onFeedbackEvent(long Id, JObject? Event = null, JObject? Status = null, JObject? Configuration = null) {
        _feedbackCallbacks[Id](Event ?? Status ?? Configuration!);
    }

    public Task<T> Get<T>(params string[] path) {
        return _jsonRpc.InvokeWithParameterObjectAsync<T>("xGet", new { Path = path });
    }

    public Task<T> Query<T>(params string[] path) {
        return _jsonRpc.InvokeWithParameterObjectAsync<T>("xQuery", new { Path = path });
    }

    public Task<bool> Set(IEnumerable<string> path, object value) {
        return _jsonRpc.InvokeWithParameterObjectAsync<bool>("xSet", new { Path = path, Value = value });
    }

    public Task<T> Command<T>(IEnumerable<string> path, object? parameters = null) {
        return _jsonRpc.InvokeWithParameterObjectAsync<T>(GetCommandMethod(path), parameters);
    }

    public Task Command(IEnumerable<string> path, object? parameters = null) {
        return _jsonRpc.InvokeWithParameterObjectAsync(GetCommandMethod(path), parameters);
    }

    private static string GetCommandMethod(IEnumerable<string> path) {
        string method = string.Join("/", path
            .SkipWhile((s, i) => i == 0 && (string.Equals(s, "Command", StringComparison.InvariantCultureIgnoreCase) || string.Equals(s, "xCommand", StringComparison.InvariantCultureIgnoreCase)))
            .Prepend("xCommand"));
        return method;
    }

    public async Task<long> Subscribe<T>(IEnumerable<string> path, Action<T> callback, bool notifyCurrentValue = false) {
        long id = await Subscribe(path, notifyCurrentValue).ConfigureAwait(false);
        _feedbackCallbacks[id] = jobject => { callback(jobject.ToObject<T>()!); };
        return id;
    }

    public async Task<long> Subscribe(IEnumerable<string> path, Action callback) {
        long id = await Subscribe(path).ConfigureAwait(false);
        _feedbackCallbacks[id] = _ => { callback(); };
        return id;
    }

    public async Task<long> Subscribe(IEnumerable<string> path, Action<JObject> callback, bool notifyCurrentValue = false) {
        long id = await Subscribe(path, notifyCurrentValue).ConfigureAwait(false);
        _feedbackCallbacks[id] = callback;
        return id;
    }

    private async Task<long> Subscribe(IEnumerable<string> path, bool notifyCurrentValue = false) {
        IDictionary<string, object> subscription = await _jsonRpc
            .InvokeWithParameterObjectAsync<IDictionary<string, object>>("xFeedback/Subscribe", new { Query = path, NotifyCurrentValue = notifyCurrentValue }).ConfigureAwait(false);
        return (long) subscription["Id"];
    }

    public Task<bool> Unsubscribe(long subscriptionId) {
        _feedbackCallbacks.Remove(subscriptionId);
        return _jsonRpc.InvokeWithParameterObjectAsync<bool>("xFeedback/Unsubscribe", new { Id = subscriptionId });
    }

    public void Dispose() {
        _webSocket.Dispose();
        _jsonRpc.Dispose();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync() {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public async Task<T> GetConfigurationOrStatus<T>(string[] path) {
        return await Get<T>(path).ConfigureAwait(false);
    }

    public async Task SetConfiguration(string[] path, object newValue) {
        await Set(path, newValue).ConfigureAwait(false);
    }

    public async Task<IDictionary<string, object?>> CallMethod(IEnumerable<string> path, IDictionary<string, object?>? parameters) {
        return await Command<IDictionary<string, object?>>(path, parameters).ConfigureAwait(false);
    }

}