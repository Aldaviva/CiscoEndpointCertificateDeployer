using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Threading.Tasks;
using System.Xml.Linq;
using jaytwo.FluentUri;

namespace CiscoEndpointDocumentationApiExtractor;

public partial class Txas: XApi {

    private static readonly CookieContainer DEFAULT_COOKIE_JAR = new();
    private static readonly XAttribute      COMMAND            = new("command", "True");

    public string hostname { get; }
    public string username { get; }
    private SecureString password { get; }

    private readonly Uri  baseUri;
    private readonly bool startedSession = false;

    private Lazy<HttpClient> _httpClient = new(() => new HttpClient(new SocketsHttpHandler {
        AllowAutoRedirect       = true,
        CookieContainer         = DEFAULT_COOKIE_JAR,
        MaxConnectionsPerServer = 10,
        UseCookies              = true
    }) {
        Timeout = TimeSpan.FromSeconds(15)
    });

    public HttpClient httpClient {
        get => _httpClient.Value;
        set {
            if (_httpClient.IsValueCreated && _httpClient.Value == value) {
                _httpClient.Value.Dispose();
                _httpClient = new Lazy<HttpClient>(value);
            }
        }
    }

    public Txas(string hostname, string username, SecureString password) {
        this.hostname = hostname;
        this.username = username;
        this.password = password.Copy();

        baseUri = new UriBuilder("https", hostname, -1).Uri;
    }

    public Txas(string hostname, string username, string password): this(hostname, username, ((Func<SecureString>) (() => {
        SecureString secureString = new();
        foreach (char c in password) {
            secureString.AppendChar(c);
        }

        secureString.MakeReadOnly();
        return secureString;
    }))()) { }

    private void Dispose(bool disposing) {
        if (disposing) {
            password.Dispose();
            if (_httpClient.IsValueCreated) {
                _httpClient.Value.Dispose();
            }

            _httpClient = null!;
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Txas() {
        Dispose(false);
    }

    public async ValueTask DisposeAsync() {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore() {
        if (startedSession) {
            await signOut().ConfigureAwait(false);
        }
    }

    public Task signOut() {
        return httpClient.PostAsync(baseUri.WithPath("/xmlapi/session/end"), null!);
    }

    // public void Dispose() {
    //     if (_httpClient.IsValueCreated) {
    //         _httpClient.Value.Dispose();
    //     }
    //
    //     password.Dispose();
    // }
    //
    // // public Task 
    // public ValueTask DisposeAsync() {
    //     throw new NotImplementedException();
    // }
    //
    // public async Task Dispose(bool skipSigningOut) {
    //     if (!skipSigningOut) {
    //
    //     }
    // }

    private Uri getTxasUri(string? readPath = null) {
        if (readPath is not null) {
            return baseUri.WithPath("/getxml").WithQueryParameter("location", readPath);
        } else {
            return baseUri.WithPath("putxml");
        }
    }

    private static XDocument createCommand(IEnumerable<string> commandParts, IDictionary<string, object>? parameters = null) {
        XContainer commandEl = commandParts.Aggregate((XContainer) new XDocument(), (parentEl, command) => {
            XElement childEl = new(command);
            parentEl.Add(childEl);
            return childEl;
        });

        if (commandEl.Document?.Root?.Name.LocalName == "xCommand") {
            commandEl.Add(COMMAND);
        }

        commandEl.Add(parameters?.Select(pair => new XElement(pair.Key, pair.Value.ToString())) ?? Enumerable.Empty<XElement>());

        return commandEl.Document!;
    }

    private class TxasRequestContent: StreamContent {

        public TxasRequestContent(XDocument requestDoc): this(requestDoc, new MemoryStream()) { }

        private TxasRequestContent(XDocument requestDoc, Stream stream): base(stream) {
            Headers.ContentType = new MediaTypeHeaderValue("text/xml");

            requestDoc.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
        }

    }

    private async Task<T> getConfigurationOrStatus<T>(string[] path) {
        using HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, getTxasUri('/' + string.Join('/', path)))).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        XDocument responseDoc                  = await response.Content.readFromXmlAsync().ConfigureAwait(false);
        XElement  findDescendentByElementNames = responseDoc.findDescendentByElementNames(path)!;
        string    rawValue                     = findDescendentByElementNames.Value;
        return typeof(T) switch {
            { } t when t == typeof(string) => (T) (object) rawValue,
            { } t when t == typeof(int)    => (T) (object) int.Parse(rawValue),
            { IsEnum: true }               => (T) Enum.Parse(typeof(T), rawValue),
            _                              => throw new ArgumentOutOfRangeException(nameof(T), typeof(T), "Cannot convert string to specified generic type " + typeof(T).Name)
        };
    }

    private Task setConfiguration(string[] path, object newValue) {
        return callMethod(path.SkipLast(1), new Dictionary<string, object> {
            { path.Last(), newValue.ToString()! }
        });
    }

    private async Task<XElement> callMethod(IEnumerable<string> path, IDictionary<string, object> parameters) {
        using HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, getTxasUri()) {
            Content = new TxasRequestContent(createCommand(path, parameters))
        }).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        XDocument responseDoc = await response.Content.readFromXmlAsync().ConfigureAwait(false);
        return responseDoc.Root!.Elements().First();
    }

}