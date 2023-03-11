using System.Diagnostics;
using CiscoEndpointCertificateDeployer.Certificates;
using CiscoEndpointCertificateDeployer.Clients;
using CiscoEndpointCertificateDeployer.Data;
using CiscoEndpointCertificateDeployer.Deploy;
using CiscoEndpointCertificateDeployer.Exceptions;

namespace CiscoEndpointCertificateDeployer;

internal static class MainClass {

    private static async Task Main(string[] args) {
        if (args.Length < 4) {

            string selfExeFilename = Process.GetCurrentProcess().ProcessName; //Assembly.GetCallingAssembly().Location fails for single-file EXE deployments
            Console.WriteLine($"usage example:\n\t\"{selfExeFilename}\" \"C:\\certificate.pfx\" 192.168.1.100 admin CISCO");
            return;
        }

        string pfxFilename      = args.ElementAt(0);
        string endpointHost     = args.ElementAt(1);
        string endpointUsername = args.ElementAt(2);
        string endpointPassword = args.ElementAt(3);

        Endpoint endpoint = new(endpointHost, endpointUsername, endpointPassword);
        await deploy(pfxFilename, endpoint);
    }

    private static async Task deploy(string pfxFilename, Endpoint endpoint) {
        Deployer deployer = new CeDeployer(endpoint);
        try {

            try {
                try {
                    await deployer.logIn();
                } catch (CiscoException.WrongOsMajorVersionDeployer) {
                    deployer.Dispose();
                    deployer = new TcDeployer(endpoint);
                    await deployer.logIn();
                }
            } catch (CiscoException.AuthenticationFailed e) {
                Console.WriteLine(e.Message);
                return;
            }

            (string pemContents, string fingerprintSha1) = CertificateService.convertPfxChainFileToPem(pfxFilename);
            // await File.WriteAllTextAsync(Path.ChangeExtension(pfxFilename, "pem"), pemContents);
            Console.WriteLine($"fingerprint (SHA-1): {fingerprintSha1}");

            await deployer.uploadCertificate(pemContents);
            await deployer.activateCertificate(fingerprintSha1, ServicePurpose.HTTPS);
            //TODO delete expired certificates
            await deployer.restartWebServer();

        } finally {
            deployer.Dispose();
        }
    }

}