using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CiscoEndpointCertificateDeployer {

    public class TcDeployer: BaseDeployer {

        private string? csrfToken { get; set; }

        public TcDeployer(Endpoint endpoint): base(endpoint) { }

        protected override bool isHttpAndHttpsConfigurationSeparate => true;

        /// <exception cref="CiscoException">if authentication fails</exception>
        public override async Task logIn() {
            if (isDisposed) {
                throw new ObjectDisposedException($"{nameof(TcDeployer)} instance has already been disposed and cannot be reused.");
            }

            FormUrlEncodedContent requestBody = new((IEnumerable<KeyValuePair<string?, string?>>) new Dictionary<string, string> {
                { "username", endpoint.username },
                { "password", endpoint.password }
            });

            using HttpResponseMessage response = await httpClient.PostAsync(new UriBuilder(endpointBaseUri) { Path = "/web/signin/open" }.Uri, requestBody);

            JsonElement result = default;
            if (response.IsSuccessStatusCode && ((await response.Content.ReadFromJsonAsync<JsonDocument>())?.RootElement.TryGetProperty("result", out result) ?? false) &&
                result.ValueKind == JsonValueKind.String && result.GetString() == "ok") {
                //logged in, cookie has been set

                using HttpResponseMessage csrfResponse = await httpClient.GetAsync(new UriBuilder(endpointBaseUri) { Path = "/web/security/cert" }.Uri);

                string csrfResponseBody = await csrfResponse.Content.ReadAsStringAsync();
                Match  csrfMatch        = Regex.Match(csrfResponseBody, @"\bvega\.csrfToken\s*=\s*['""](?<csrfToken>\w+)['""]");
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

            HttpContent pemContent;
            pemContent = new StringContent(pemCertificate, Encoding.UTF8, "application/octet-stream");
            // pemContent = new(Encoding.UTF8.GetBytes(pemCertificate));
            // pemContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            MultipartFormDataContent requestBody = new() {
                { pemContent, "certificate", "ciscocert.pem" },
                { new ByteArrayContent(Array.Empty<byte>()), "privkey", string.Empty },
                { new ByteArrayContent(Array.Empty<byte>()), "passphrase" },
                { new StringContent(csrfToken ?? "no token", Encoding.UTF8), "token" }
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

    }

}