using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using CiscoEndpointCertificateDeployer.Clients;
using CiscoEndpointCertificateDeployer.Data;
using CiscoEndpointCertificateDeployer.Exceptions;

namespace CiscoEndpointCertificateDeployer.Deploy;

public abstract class BaseDeployer(Endpoint endpoint): Deployer {

    protected readonly Endpoint   endpoint        = endpoint;
    protected readonly Uri        endpointBaseUri = new UriBuilder("https", endpoint.host, 443).Uri;
    protected readonly HttpClient httpClient      = new(new SocketsHttpHandler {
        Credentials       = new NetworkCredential(endpoint.username, endpoint.password),
        PreAuthenticate   = false,
        CookieContainer   = new CookieContainer(),
        AllowAutoRedirect = false,
        //proxy messes up HEAD checks to see if HTTP server is still up, because Fiddler will respond with an HTML error message when there's an upstream SocketException
        SslOptions = new SslClientAuthenticationOptions {
            RemoteCertificateValidationCallback = delegate { return true; },
            EnabledSslProtocols                 = SslProtocols.Tls12 | SslProtocols.Tls13
        }
    });

    protected bool loggedIn { get; set; }
    private volatile bool disposed;
    protected bool isDisposed => disposed;

    public abstract Task logIn();
    public abstract Task uploadCertificate(string   pemCertificate);
    public abstract Task activateCertificate(string certificateFingerprintSha1, ServicePurpose servicePurpose);
    public abstract Task<IEnumerable<CiscoCertificate>> listCertificates();
    public abstract Task deleteCertificate(string certificateFingerprintSha1);

    //proxy messes up HEAD checks to see if HTTP server is still up, because Fiddler will respond with an HTML error message when there's an upstream SocketException

    /// <summary>
    /// <para><c>true</c>:  TC7 and earlier use two different <c>NetworkServices</c> configurations for HTTP and HTTPS</para>
    /// <para><c>false</c>: CE8 and later use one <c>NetworkServices</c> configuration for HTTP and HTTPS</para>
    /// </summary>
    protected abstract bool isHttpAndHttpsConfigurationSeparate { get; }

    public virtual async Task restartWebServer() {
        ensureLoggedInAndNotDisposed();

        using XacliClient xacliClient = new(endpoint);
        xacliClient.logIn();

        Console.WriteLine("turning off HTTP");
        xacliClient.writeLine(createWebServerEnabledCommandString(false)); //takes about 3.5 seconds to take effect, including the socketexception
        Console.WriteLine(xacliClient.waitForOkResponse());

        await Task.Delay(TimeSpan.FromSeconds(4));

        // repeatedly test if web server is offline until it returns an error
        bool isOnline = true;
        while (isOnline) {
            try {
                using HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, endpointBaseUri));
                isOnline = true;
                Console.WriteLine("http still online");
                await Task.Delay(TimeSpan.FromSeconds(1));
            } catch (HttpRequestException e) when (e.InnerException is SocketException) {
                isOnline = false;
                Console.WriteLine("http offline");
            } catch (HttpRequestException e) {
                Console.WriteLine(e);
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(4));

        Console.WriteLine("turning on HTTP");
        xacliClient.writeLine(createWebServerEnabledCommandString(true)); //could get and remember the old value
        Console.WriteLine(xacliClient.waitForOkResponse());

        xacliClient.writeLine("bye");
    }

    private string createWebServerEnabledCommandString(bool enable) {
        (string networkService, string serviceMode) = (isHttpAndHttpsConfigurationSeparate, enable) switch {
            (false, true)  => ("HTTP", "HTTPS"), // CE on (serviceMode can also be HTTP+HTTPS, which was the old default in CE9.3 and earlier)
            (false, false) => ("HTTP", "Off"),   // CE off
            (true, true)   => ("HTTPS", "On"),   // TC on
            (true, false)  => ("HTTPS", "Off")   // TC off
        };

        return $"xconfiguration NetworkServices {networkService} Mode: {serviceMode}";
    }

    /// <exception cref="CiscoException">if not logged in yet</exception>
    /// <exception cref="ObjectDisposedException">if already disposed</exception>
    protected void ensureLoggedInAndNotDisposed() {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!loggedIn) {
            throw new CiscoException("Not logged in yet, call BaseDeployer.logIn() first.");
        }
    }

    private void Dispose(bool disposing) {
        if (disposing) {
            disposed = true;
            loggedIn = false;
            httpClient.Dispose();
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

}