using System.Net;
using System.Xml.Linq;
using CiscoEndpointCertificateDeployer.Clients;
using CiscoEndpointCertificateDeployer.Data;
using CiscoEndpointCertificateDeployer.Exceptions;

namespace CiscoEndpointCertificateDeployer.Deploy;

public class CeDeployer: BaseDeployer {

    private readonly Stream stdout = Console.OpenStandardOutput(1024 * 1024);

    public CeDeployer(Endpoint endpoint): base(endpoint) { }

    protected override bool isHttpAndHttpsConfigurationSeparate => false;

    private Uri putXml() => new UriBuilder(endpointBaseUri) { Path = "/putxml" }.Uri;

    /// <exception cref="CiscoException">if logging in fails</exception>
    public override async Task logIn() {
        if (isDisposed) {
            throw new ObjectDisposedException($"{nameof(CeDeployer)} instance has already been disposed and cannot be reused.");
        }

        using HttpResponseMessage response = await httpClient.PostAsync(new UriBuilder(endpointBaseUri) { Path = "/xmlapi/session/begin" }.Uri, null!);

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (response.StatusCode) {
            case HttpStatusCode.NoContent:
                Console.WriteLine("Logged in with XMLAPI session.");
                loggedIn = true;
                break;
            case HttpStatusCode.Unauthorized:
                throw new CiscoException.AuthenticationFailed("Incorrect credentials");
            case HttpStatusCode.Found:
            case HttpStatusCode.SeeOther:
            case HttpStatusCode.TemporaryRedirect:
                throw new CiscoException.WrongOsMajorVersionDeployer($"XMLAPI sessions are not supported (endpoint is too old), please use {nameof(TcDeployer)} instead.");
            default:
                throw new CiscoException("Failed to authenticate", new ArgumentOutOfRangeException(nameof(response.StatusCode), (int) response.StatusCode, "Unhandled auth response status code"));
        }
    }

    public override async Task uploadCertificate(string pemCertificate) {
        ensureLoggedInAndNotDisposed();

        XDocument command = TXAS.createCommand("Command Security Certificates Services Add", new Dictionary<string, string> { { "body", pemCertificate } });

        using HttpResponseMessage response = await httpClient.PostAsync(putXml(), new TxasRequestContent(command));
        await response.Content.CopyToAsync(stdout);
        if (response.IsSuccessStatusCode) {
            Console.WriteLine("Uploaded certificate");
        } else {
            throw new CiscoException($"Failed to upload certificate: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public override async Task activateCertificate(string certificateFingerprintSha1, ServicePurpose servicePurpose) {
        ensureLoggedInAndNotDisposed();

        XDocument command = TXAS.createCommand(new[] { "Command", "Security", "Certificates", "Services", "Activate" }, new Dictionary<string, string> {
            { "Fingerprint", certificateFingerprintSha1.ToLowerInvariant() },
            { "Purpose", servicePurpose.txasName() }
        });

        using HttpResponseMessage response = await httpClient.PostAsync(putXml(), new TxasRequestContent(command));
        await response.Content.CopyToAsync(stdout);
        if (response.IsSuccessStatusCode) {
            Console.WriteLine($"Activated certificate {certificateFingerprintSha1}");
        } else {
            throw new CiscoException($"Failed to activate certificate: {await response.Content.ReadAsStringAsync()}");
        }
    }

}