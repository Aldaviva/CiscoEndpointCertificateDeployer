using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CiscoEndpointDocumentationApiExtractor;

public interface XapiTransport: IDisposable, IAsyncDisposable {

    string hostname { get; }
    string username { get; }

    Task signOut();

    Task<T> getConfigurationOrStatus<T>(string[] path);

    Task setConfiguration(string[] path, object newValue);

    Task<XElement> callMethod(IEnumerable<string> path, IDictionary<string, object> parameters);

}