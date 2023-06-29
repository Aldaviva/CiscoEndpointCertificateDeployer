using CSxAPI.Enums;
using System.Collections.Concurrent;

namespace CSxAPI;

/// <summary>
/// Keep track of which numeric subscription ID (from Cisco) belongs to which callback (from the library consumer).
/// </summary>
internal class EventSubscriber {

    private readonly WebSocketXapi                      _transport;
    private readonly ConcurrentDictionary<object, long> _subscribers = new(); // key is EventCallback<?>

    public EventSubscriber(WebSocketXapi transport) {
        _transport = transport;
    }

    public async Task Subscribe<TSerialized, TDeserialized>(IEnumerable<string> path, EventCallback<TDeserialized> callback, Func<TSerialized, TDeserialized> deserialize, bool notifyCurrentValue = false) {
        long subscriptionId = await _transport.Subscribe<TSerialized>(path, payload => callback(deserialize(payload)), notifyCurrentValue).ConfigureAwait(false);
        _subscribers[callback] = subscriptionId;
    }

    public async Task Unsubscribe<T>(EventCallback<T> callback) {
        if (_subscribers.TryRemove(callback, out long subscriptionId)) {
            await _transport.Unsubscribe(subscriptionId).ConfigureAwait(false);
        }
    }

    /*public void Test(EventCallback<ConfigurationAppsWallpaperBundlesHalfwakeImage> callback) {
        Subscribe<string, ConfigurationAppsWallpaperBundlesHalfwakeImage>(new[] { "a", "b", "c" }, callback, serialized => ValueSerializer.Deserialize<ConfigurationAppsWallpaperBundlesHalfwakeImage>(serialized)).Wait();
    }

    public void Test2(EventCallback<int?> callback) {
        Subscribe<string, int?>(new[] { "a", "b", "c" }, callback, serialized => ValueSerializer.Deserialize(serialized, "Off")).Wait();
    }

    public void Test3(EventCallback<int> callback) {
        Subscribe<int, int>(new[] { "a", "b", "c" }, callback, serialized => ValueSerializer.Deserialize(serialized)).Wait();
    }

    public void Test4(EventCallback<string> callback) {
        Subscribe<string, string>(new[] { "a", "b", "c" }, callback, serialized => ValueSerializer.Deserialize(serialized)).Wait();
    }*/


}

public delegate void EventCallback<in T>(T newValue);