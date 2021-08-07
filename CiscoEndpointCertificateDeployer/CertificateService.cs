using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CiscoEndpointCertificateDeployer {

    internal static class CertificateService {

        /// <exception cref="ArgumentException">if pfx was exported without a private key</exception>
        public static (string pem, string fingerprint) convertPfxChainFileToPem(string pfxFilename) {
            string? fingerprint = null;

            X509Certificate2Collection pfxCertificates = new();
            pfxCertificates.Import(pfxFilename, null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

            StringBuilder pemContents = new();
            foreach (X509Certificate2 pfxCertificate in pfxCertificates.Cast<X509Certificate2>().Reverse()) { //make sure leaf is first because this is what Cisco tries to use
                pemContents.Append(PemEncoding.Write("CERTIFICATE", pfxCertificate.RawData)).AppendLine().AppendLine();

                AsymmetricAlgorithm? privateKey = pfxCertificate.PrivateKey switch {
                    null  => null,
                    DSA   => pfxCertificate.GetDSAPrivateKey(),
                    ECDsa => pfxCertificate.GetECDsaPrivateKey(),
                    RSA   => pfxCertificate.GetRSAPrivateKey(), // RSA is what Let's Encrypt/CertifyTheWeb seems to be generating
                    _     => throw new ArgumentOutOfRangeException($"Unhandled private key algorithm {pfxCertificate.PrivateKey.GetType()}")
                };

                if (privateKey?.ExportPkcs8PrivateKey() is { } privateKeyBytes) {
                    pemContents.Append(PemEncoding.Write("PRIVATE KEY", privateKeyBytes)).AppendLine().AppendLine();
                    fingerprint = pfxCertificate.Thumbprint.ToLowerInvariant();
                }
            }

            if (fingerprint is not null) {
                return (pemContents.ToString(), fingerprint);
            } else {
                throw new ArgumentException("PFX certificate does not contain a private key", nameof(pfxFilename));
            }
        }

    }

}