using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace CiscoEndpointDocumentationApiExtractor;

public interface XApi: IDisposable, IAsyncDisposable {

    string hostname { get; }
    string username { get; }
    HttpClient httpClient { get; set; }

    Txas.Commands.XConfiguration xConfiguration { get; }
    Txas.Commands.XStatus xStatus { get; }
    Txas.Commands.XCommand xCommand { get; }

    Task signOut();

}