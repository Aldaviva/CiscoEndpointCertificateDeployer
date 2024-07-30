using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CiscoEndpointCertificateDeployer.Clients;
using CiscoEndpointCertificateDeployer.Data;
using CiscoEndpointCertificateDeployer.Exceptions;

namespace CiscoEndpointCertificateDeployer.Deploy;

public partial class TcDeployer(Endpoint endpoint): BaseDeployer(endpoint) {

    private static readonly Encoding UTF8_SERIALIZING = new UTF8Encoding(false, true);

    [GeneratedRegex("\\bvega\\.csrfToken\\s*=\\s*['\"](?<csrfToken>\\w+)['\"]")]
    private static partial Regex csrfTokenPattern();

    private string? csrfToken { get; set; }

    protected override bool isHttpAndHttpsConfigurationSeparate => true;

    /// <exception cref="CiscoException">if authentication fails</exception>
    public override async Task logIn() {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        FormUrlEncodedContent requestBody = new((IEnumerable<KeyValuePair<string?, string?>>) new Dictionary<string, string> {
            { "username", endpoint.username },
            { "password", endpoint.password }
        });

        using HttpResponseMessage response = await httpClient.PostAsync(new UriBuilder(endpointBaseUri) { Path = "/web/signin/open" }.Uri, requestBody);

        if (response.IsSuccessStatusCode && ((await response.Content.ReadFromJsonAsync<JsonDocument>())?.RootElement.TryGetProperty("result", out JsonElement result) ?? false) &&
            result.ValueKind == JsonValueKind.String && result.GetString() == "ok") {
            //logged in, cookie has been set

            using HttpResponseMessage csrfResponse = await httpClient.GetAsync(new UriBuilder(endpointBaseUri) { Path = "/web/security/cert" }.Uri);

            string csrfResponseBody = await csrfResponse.Content.ReadAsStringAsync();
            Match  csrfMatch        = csrfTokenPattern().Match(csrfResponseBody);
            csrfToken = csrfMatch.Success ? csrfMatch.Groups["csrfToken"].Value : null;
            loggedIn  = true;
            Console.WriteLine("Logged in with web session.");
        } else {
            throw new CiscoException("Authentication to TC7 endpoint failed");
        }
    }

    /// <exception cref="CiscoException">if upload does not result in a 303 to the certificate list page</exception>
    public override async Task uploadCertificate(string pemCertificate) {
        ensureLoggedInAndNotDisposed();

        HttpContent pemContent = new StringContent(pemCertificate, UTF8_SERIALIZING, "application/octet-stream");

        MultipartFormDataContent requestBody = new() {
            { pemContent, "certificate", "ciscocert.pem" },
            { new ByteArrayContent([]), "privkey", string.Empty },
            { new ByteArrayContent([]), "passphrase" },
            { new StringContent(csrfToken ?? "no token", UTF8_SERIALIZING), "token" }
        };

        using HttpResponseMessage response = await httpClient.PostAsync(new UriBuilder(endpointBaseUri) { Path = "/web/security/certAdd" }.Uri, requestBody);

        if (response.StatusCode == HttpStatusCode.SeeOther) {
            Console.WriteLine("Uploaded certificate");
        } else {
            throw new CiscoException("Failed to upload certificate");
        }
    }

    public override async Task activateCertificate(string certificateFingerprintSha1, ServicePurpose servicePurpose) {
        ensureLoggedInAndNotDisposed();

        Dictionary<string, string?> requestBody = new() {
            { "id", servicePurpose.vegaIdTc() },
            { "name", servicePurpose.vegaNameTc() },
            { "success", "success" },
            { "certificate", certificateFingerprintSha1.ToUpperInvariant() }
        };

        using HttpResponseMessage response = await httpClient.PutAsJsonAsync(new UriBuilder(endpointBaseUri) { Path = "/web/api/certificates/service/https_server" }.Uri, requestBody);

        if (response.IsSuccessStatusCode) {
            Console.WriteLine($"Activated certificate {certificateFingerprintSha1}");
        } else {
            throw new CiscoException("Failed to activate new certificate");
        }
    }

    public override Task<IEnumerable<CiscoCertificate>> listCertificates() {
        // not implemented
        return Task.FromResult(Enumerable.Empty<CiscoCertificate>());
    }

    public override Task deleteCertificate(string certificateFingerprintSha1) {
        // not implemented
        return Task.CompletedTask;
    }

}