namespace CSxAPI.Transport;

public interface IXapiTransport: IDisposable, IAsyncDisposable {

    public string Hostname { get; }
    public string Username { get; }

    Task<T> GetConfigurationOrStatus<T>(string[] path);

    Task SetConfiguration(string[] path, object newValue);

    Task<IDictionary<string, object?>> CallMethod(IEnumerable<string> path, IDictionary<string, object?>? parameters);

}