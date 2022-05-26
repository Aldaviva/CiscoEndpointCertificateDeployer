using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace CiscoWebsocketXapi;

[SuppressMessage("ReSharper", "CoVariantArrayConversion")]
public class JsonRpcXapi: XApi {

    private readonly string          _hostname;
    private readonly ClientWebSocket _webSocket = new();
    private readonly JsonRpc         _jsonRpc;

#if NETCOREAPP3_0_OR_GREATER || NET20_OR_GREATER
    private readonly TraceListener _consoleTraceListener = new ConsoleTraceListener();
#else
    private readonly TraceListener _consoleTraceListener = new TextWriterTraceListener(Console.Out);
#endif

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

    private readonly IDictionary<long, Action<JObject>> _feedbackCallbacks = new Dictionary<long, Action<JObject>>();

    public JsonRpcXapi(string hostname, string username, string password, bool allowSelfSigned = false) {
        _hostname = hostname;
        _webSocket.Options.SetRequestHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
        if (allowSelfSigned) {
#if NETSTANDARD2_1_OR_GREATER
            _webSocket.Options.RemoteCertificateValidationCallback = delegate { return true; };
#else
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
#endif
        }

        _jsonRpc = new JsonRpc(new WebSocketMessageHandler(_webSocket));
    }

    public async Task connect(CancellationToken? cancellationToken = null) {
        Uri uri = new UriBuilder("wss", _hostname, -1, "ws").Uri;
        await _webSocket.ConnectAsync(uri, cancellationToken ?? CancellationToken.None);
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

    public Task<T> Get<T>(params string[] path) => Get<T>((object[]) path);

    public Task<T> Get<T>(params object[] path) {
        return _jsonRpc.InvokeWithParameterObjectAsync<T>("xGet", new { Path = path });
    }

    public Task<T> Query<T>(params string[] path) => Query<T>((object[]) path);

    public Task<T> Query<T>(params object[] path) {
        return _jsonRpc.InvokeWithParameterObjectAsync<T>("xQuery", new { Path = path });
    }

    public Task<bool> Set(IEnumerable<string> path, object value) => Set((object[]) path, value);

    public Task<bool> Set(IEnumerable<object> path, object value) {
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

    public Task<long> Subscribe<T>(string[] path, Action<T>       callback, bool notifyCurrentValue = false) => Subscribe((object[]) path, callback, notifyCurrentValue);
    public Task<long> Subscribe(string[]    path, Action<JObject> callback, bool notifyCurrentValue = false) => Subscribe((object[]) path, callback, notifyCurrentValue);

    public async Task<long> Subscribe<T>(object[] path, Action<T> callback, bool notifyCurrentValue = false) {
        long id = await Subscribe(path, notifyCurrentValue);
        _feedbackCallbacks[id] = jobject => { callback(jobject.ToObject<T>()!); };
        return id;
    }

    public async Task<long> Subscribe(object[] path, Action<JObject> callback, bool notifyCurrentValue = false) {
        long id = await Subscribe(path, notifyCurrentValue);
        _feedbackCallbacks[id] = callback;
        return id;
    }

    private async Task<long> Subscribe(IEnumerable<object> path, bool notifyCurrentValue = false) {
        var subscription = await _jsonRpc.InvokeWithParameterObjectAsync<IDictionary<string, object>>("xFeedback/Subscribe", new { Query = path, NotifyCurrentValue = notifyCurrentValue });
        return (long) subscription["Id"];
    }

    public Task<bool> Unsubscribe(long subscriptionId) {
        _feedbackCallbacks.Remove(subscriptionId);
        return _jsonRpc.InvokeWithParameterObjectAsync<bool>("xFeedback/Unsubscribe", new { Id = subscriptionId });
    }

    public void Dispose() {
        _webSocket.Dispose();
        _jsonRpc.Dispose();
    }

}