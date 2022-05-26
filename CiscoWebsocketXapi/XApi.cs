using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CiscoWebsocketXapi;

public interface XApi: IDisposable {

    Task<T> Get<T>(params string[] path);
    Task<T> Get<T>(params object[] path);

    Task<T> Query<T>(params string[] path);
    Task<T> Query<T>(params object[] path);

    Task<bool> Set(IEnumerable<string> path, object value);
    Task<bool> Set(IEnumerable<object> path, object value);

    Task<T> Command<T>(IEnumerable<string> path, object? parameters = null);
    Task Command(IEnumerable<string>       path, object? parameters = null);

    Task connect(CancellationToken? cancellationToken = null);

    Task<long> Subscribe<T>(string[] path, Action<T> callback, bool notifyCurrentValue = false);

    Task<long> Subscribe(string[] path, Action<JObject> callback, bool notifyCurrentValue = false);

    Task<long> Subscribe<T>(object[] path, Action<T> callback, bool notifyCurrentValue = false);

    Task<long> Subscribe(object[] path, Action<JObject> callback, bool notifyCurrentValue = false);

    Task<bool> Unsubscribe(long subscriptionId);

}