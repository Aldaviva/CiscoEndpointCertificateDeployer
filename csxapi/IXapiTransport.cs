namespace CSxAPI;

public interface IXapiTransport: IDisposable, IAsyncDisposable {

    string Hostname { get; }
    string Username { get; }

    Task<T> GetConfigurationOrStatus<T>(string[] path);

    Task SetConfiguration(string[] path, object newValue);

    Task<IDictionary<string, object?>> CallMethod(IEnumerable<string> path, IDictionary<string, object?>? parameters);

}