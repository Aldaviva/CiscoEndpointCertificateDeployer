using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CiscoEndpointCertificateDeployer.Certificates;

public static class CertificateService {

    /// <exception cref="ArgumentException">if pfx was exported without a private key</exception>
    public static (string pem, string fingerprint) convertPfxChainFileToPem(string pfxFilename) {
        string? fingerprint = null;

        X509Certificate2Collection pfxCertificates = [];
        pfxCertificates.Import(pfxFilename, null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

        StringBuilder pemContents = new();
        foreach (X509Certificate2 pfxCertificate in pfxCertificates.Reverse()) { //make sure leaf is first because this is what Cisco tries to use
            pemContents.Append(PemEncoding.Write("CERTIFICATE", pfxCertificate.RawData)).AppendLine().AppendLine();

            AsymmetricAlgorithm? privateKey = pfxCertificate.GetDSAPrivateKey() as AsymmetricAlgorithm
                ?? pfxCertificate.GetECDsaPrivateKey() as AsymmetricAlgorithm
                ?? pfxCertificate.GetRSAPrivateKey() as AsymmetricAlgorithm
                ?? pfxCertificate.GetECDiffieHellmanPrivateKey();

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