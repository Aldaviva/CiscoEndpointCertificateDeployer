﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CiscoEndpointCertificateDeployer {

    internal static class MainClass {

        private static async Task Main(string[] args) {
            if (args.Length < 4) {
                string selfExeFilename = Path.GetFileName(Process.GetCurrentProcess().MainModule!.FileName)!;
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
            (string pemContents, string fingerprintSha1) = CertificateService.convertPfxChainFileToPem(pfxFilename);
            // await File.WriteAllTextAsync(Path.ChangeExtension(pfxFilename, "pem"), pemContents);
            Console.WriteLine($"fingerprint (SHA-1): {fingerprintSha1}");

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

                await deployer.uploadCertificate(pemContents);
                await deployer.activateCertificate(fingerprintSha1, ServicePurpose.HTTPS);
                await deployer.restartWebServer();

            } finally {
                deployer.Dispose();
            }
        }

    }

}