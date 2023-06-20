using System.Xml.Linq;

namespace CSxAPI;

public interface IXapiTransport: IDisposable, IAsyncDisposable {

    string Hostname { get; }
    string Username { get; }

    Task SignOut();

    Task<T> GetConfigurationOrStatus<T>(string[] path);

    Task SetConfiguration(string[] path, object newValue);

    Task<XElement> CallMethod(IEnumerable<string> path, IDictionary<string, object?>? parameters);

}