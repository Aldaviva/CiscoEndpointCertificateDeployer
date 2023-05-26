using System.Xml.Serialization;

namespace CiscoEndpointCertificateDeployer.Data;

public record CiscoCertificate {

    [XmlElement("Fingerprint")]
    public string fingerprint { get; set; } = null!;

    [XmlElement("HashingAlgorithm")]
    public string hashingAlgorithm { get; set; } = null!;

    [XmlElement("IssuerName")]
    public string issuerName { get; set; } = null!;

    [XmlElement("PublicKeySize")]
    public int publicKeySize { get; set; }

    [XmlElement("SerialNumber")]
    public string serialNumber { get; set; } = null!;

    [XmlElement("SignatureAlgorithm")]
    public string signatureAlgorithm { get; set; } = null!;

    [XmlElement("SubjectName")]
    public string subjectName { get; set; } = null!;

    [XmlElement("UsedFor")]
    public string usedFor { get; set; } = null!;

    [XmlElement("Version")]
    public int version { get; set; }

    public string notAfter { get; set; } = null!;
    public string notBefore { get; set; } = null!;

}