using System.Collections.Concurrent;
using CSxAPI.Transport;
using Newtonsoft.Json.Linq;

namespace CSxAPI;

/// <summary>
/// Keep track of which numeric subscription ID (from Cisco) belongs to which callback (from the library consumer).
/// </summary>
internal class FeedbackSubscriber {

    private readonly WebSocketXapi                      _transport;
    private readonly ConcurrentDictionary<object, long> _subscribers = new(); // key is FeedbackCallback<T>

    public FeedbackSubscriber(WebSocketXapi transport) {
        _transport = transport;
    }

    public async Task Subscribe<TSerialized, TDeserialized>(IEnumerable<string> path, FeedbackCallback<TDeserialized> callback, Func<TSerialized, TDeserialized> deserialize,
                                                            bool                notifyCurrentValue = false) {
        long subscriptionId = await _transport.Subscribe<TSerialized>(path, payload => callback(deserialize(payload)), notifyCurrentValue).ConfigureAwait(false);
        _subscribers[callback] = subscriptionId;
    }

    public async Task Subscribe<TDeserialized>(IEnumerable<string> path, FeedbackCallback<TDeserialized> callback, Func<JObject, TDeserialized> deserialize,
                                                            bool                notifyCurrentValue = false) {
        long subscriptionId = await _transport.Subscribe(path, payload => callback(deserialize(payload)), notifyCurrentValue).ConfigureAwait(false);
        _subscribers[callback] = subscriptionId;
    }

    public async Task Subscribe(IEnumerable<string> path, FeedbackCallback callback) {
        long subscriptionId = await _transport.Subscribe(path, () => callback()).ConfigureAwait(false);
        _subscribers[callback] = subscriptionId;
    }

    public async Task Unsubscribe<T>(FeedbackCallback<T> callback) {
        if (_subscribers.TryRemove(callback, out long subscriptionId)) {
            await _transport.Unsubscribe(subscriptionId).ConfigureAwait(false);
        }
    }

    public async Task Unsubscribe(FeedbackCallback callback) {
        if (_subscribers.TryRemove(callback, out long subscriptionId)) {
            await _transport.Unsubscribe(subscriptionId).ConfigureAwait(false);
        }
    }

}

public delegate void FeedbackCallback();
public delegate void FeedbackCallback<in T>(T newValue);