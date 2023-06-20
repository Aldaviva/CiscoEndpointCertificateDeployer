using System.Net;
using System.Net.Http.Headers;
using System.Security;
using System.Xml.Linq;

namespace CSxAPI;

/// <summary>
/// Send xAPI commands over TXAS (Tandberg XML API Service), which is a protocol that uses XML over HTTP.
/// </summary>
public class TxasTransport: IXapiTransport {

    private static readonly CookieContainer      DefaultCookieJar = new();
    private static readonly XAttribute           Command          = new("command", "True");
    private static readonly MediaTypeHeaderValue XmlMediaType     = new("text/xml");

    public string Hostname { get; }
    public string Username { get; }
    private SecureString Password { get; }

    private readonly Uri  _baseUri;
    private readonly bool _startedSession = false;

    private bool _shouldDisposeHttpClient = true;

    private Lazy<HttpClient> _httpClient = new(() => new HttpClient(new SocketsHttpHandler {
        AllowAutoRedirect       = true,
        CookieContainer         = DefaultCookieJar,
        MaxConnectionsPerServer = 10,
        UseCookies              = true
    }) {
        Timeout = TimeSpan.FromSeconds(15)
    });

    public HttpClient HttpClient {
        get => _httpClient.Value;
        set {
            if (_httpClient.IsValueCreated && _httpClient.Value != value) {
                _httpClient.Value.Dispose();
                _httpClient              = new Lazy<HttpClient>(() => value);
                _shouldDisposeHttpClient = true;
            }
        }
    }

    public TxasTransport(string hostname, string username, SecureString password) {
        Hostname = hostname;
        Username = username;
        Password = password.Copy();

        _baseUri = new UriBuilder("https", hostname, -1).Uri;
    }

    public TxasTransport(string hostname, string username, string password): this(hostname, username, ((Func<SecureString>) (() => {
        SecureString secureString = new();
        foreach (char c in password) {
            secureString.AppendChar(c);
        }

        secureString.MakeReadOnly();
        return secureString;
    }))()) { }

    private void Dispose(bool disposing) {
        if (disposing) {
            Password.Dispose();
            if (_httpClient.IsValueCreated && _shouldDisposeHttpClient) {
                _httpClient.Value.Dispose();
            }

            _httpClient = null!;
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~TxasTransport() {
        Dispose(false);
    }

    public async ValueTask DisposeAsync() {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore() {
        if (_startedSession) {
            await SignOut().ConfigureAwait(false);
        }
    }

    public Task SignOut() {
        return HttpClient.PostAsync(new UriBuilder(_baseUri) { Path = "/xmlapi/session/end" }.Uri, null!);
    }

    private Uri GetTxasUri(string? readPath = null) {
        if (readPath is not null) {
            return new UriBuilder(_baseUri) { Path = "/getxml", Query = "location=" + readPath }.Uri;
        } else {
            return new UriBuilder(_baseUri) { Path = "/putxml" }.Uri;
        }
    }

    private static XDocument CreateCommand(IEnumerable<string> commandParts, IDictionary<string, object?>? parameters = null) {
        XContainer commandEl = commandParts.Aggregate((XContainer) new XDocument(), (parentEl, command) => {
            XElement childEl = new(command);
            parentEl.Add(childEl);
            return childEl;
        });

        if (commandEl.Document?.Root?.Name.LocalName == "xCommand") {
            commandEl.Add(Command);
        }

        commandEl.Add(parameters?
            .Where(pair => pair.Value != null).Cast<KeyValuePair<string, object>>()
            .Select(pair => new XElement(pair.Key, pair.Value.ToString())) ?? Enumerable.Empty<XElement>());

        return commandEl.Document!;
    }

    private class TxasRequestContent: StreamContent {

        public TxasRequestContent(XDocument requestDoc): this(requestDoc, new MemoryStream()) { }

        private TxasRequestContent(XDocument requestDoc, Stream stream): base(stream) {
            Headers.ContentType = XmlMediaType;

            requestDoc.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
        }

    }

    public async Task<T> GetConfigurationOrStatus<T>(string[] path) {
        using HttpResponseMessage response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, GetTxasUri('/' + string.Join('/', path)))).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        XDocument responseDoc                  = await response.Content.ReadFromXmlAsync().ConfigureAwait(false);
        XElement  findDescendentByElementNames = responseDoc.FindDescendentByElementNames(path)!;
        string    rawValue                     = findDescendentByElementNames.Value;
        return typeof(T) switch {
            { } t when t == typeof(string) => (T) (object) rawValue,
            { } t when t == typeof(int)    => (T) (object) int.Parse(rawValue),
            { IsEnum: true }               => (T) Enum.Parse(typeof(T), rawValue),
            _                              => throw new ArgumentOutOfRangeException(nameof(T), typeof(T), "Cannot convert string to specified generic type " + typeof(T).Name)
        };
    }

    public Task SetConfiguration(string[] path, object newValue) => CallMethod(path.SkipLast(1), new Dictionary<string, object?> { { path.Last(), newValue.ToString()! } });

    public async Task<XElement> CallMethod(IEnumerable<string> path, IDictionary<string, object?>? parameters = null) {
        using HttpResponseMessage response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, GetTxasUri()) {
            Content = new TxasRequestContent(CreateCommand(path, parameters))
        }).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        XDocument responseDoc = await response.Content.ReadFromXmlAsync().ConfigureAwait(false);
        return responseDoc.Root!.Elements().First();
    }

}