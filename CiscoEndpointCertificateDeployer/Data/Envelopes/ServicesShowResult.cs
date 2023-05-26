using System.Xml.Serialization;

namespace CiscoEndpointCertificateDeployer.Data.Envelopes;

public class ServicesShowResult {

    [XmlElement("Details")]
    public List<CiscoCertificate> certificates { get; set; } = null!;

}