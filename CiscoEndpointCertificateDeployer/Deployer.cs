using System;
using System.Threading.Tasks;

namespace CiscoEndpointCertificateDeployer {

    public interface Deployer: IDisposable {

        /// <exception cref="CiscoException">if authentication fails</exception>
        public Task logIn();

        /// <exception cref="CiscoException">If the certificate upload fails</exception>
        public Task uploadCertificate(string pemCertificate);

        /// <param name="certificateFingerprintSha1">The SHA-1 fingerprint/thumbprint of the already-uploaded certificate to activate</param>
        /// <param name="servicePurpose">The service to activate the certificate on. <c>PAIRING</c>, <c>HTTP_CLIENT</c>, and <c>WEBEX_IDENTITY</c> are not available on TC.</param>
        /// <exception cref="CiscoException">If the certificate activation fails</exception>
        public Task activateCertificate(string certificateFingerprintSha1, ServicePurpose servicePurpose);

        public Task restartWebServer();

    }

}