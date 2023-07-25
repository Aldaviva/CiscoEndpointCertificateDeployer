using Newtonsoft.Json.Linq;

namespace CSxAPI.Transport;

public interface IWebSocketXapi: IXapiTransport, IWebSocketClient {

    Task<T> Get<T>(params string[] path);

    Task<T> Query<T>(params string[] path);

    Task<bool> Set(IEnumerable<string> path, object value);

    Task<T> Command<T>(IEnumerable<string> path, object? parameters = null);

    Task Command(IEnumerable<string> path, object? parameters = null);

    Task<long> Subscribe(IEnumerable<string> path, Action callback);

    Task<long> Subscribe(IEnumerable<string> path, Action<JObject> callback, bool notifyCurrentValue = false);

    Task<long> Subscribe<T>(IEnumerable<string> path, Action<T> callback, bool notifyCurrentValue = false);

    Task<bool> Unsubscribe(long subscriptionId);

}