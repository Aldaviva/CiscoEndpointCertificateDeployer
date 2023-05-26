using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using CiscoEndpointCertificateDeployer.Clients;
using CiscoEndpointCertificateDeployer.Data.Envelopes;
using Xunit;

namespace Tests {

    public class TXASTest {

        [Fact]
        public void createCommand() {
            string       actual   = TXAS.createCommand("Command Security Certificates Services Add", new Dictionary<string, string> { { "body", "abc" } }).ToString(SaveOptions.DisableFormatting);
            const string EXPECTED = @"<Command><Security><Certificates><Services><Add command=""True""><body>abc</body></Add></Services></Certificates></Security></Command>";
            Assert.Equal(EXPECTED, actual);
        }

        [Fact]
        public void createCommand2() {
            string actual = TXAS.createCommand(new[] { "Command", "Security", "Certificates", "Services", "Add" }, new Dictionary<string, string> {
                { "Fingerprint", "abc" },
                { "Purpose", ServicePurpose.HTTPS.txasName() }
            }).ToString(SaveOptions.DisableFormatting);
            const string EXPECTED =
                @"<Command><Security><Certificates><Services><Add command=""True""><Fingerprint>abc</Fingerprint><Purpose>HTTPS</Purpose></Add></Services></Certificates></Security></Command>";
            Assert.Equal(EXPECTED, actual);
        }

        [Fact]
        public void xmlTest() {
            XDocument doc = XDocument.Parse("""
                <?xml version="1.0"?>
                <Command>
                  <ServicesShowResult status="OK">
                    <Details item="1" maxOccurrence="n">
                      <Fingerprint>ce4eb947c2c17d58bbc08303bf278464beebf2af</Fingerprint>
                      <HashingAlgorithm>sha256WithRSAEncryption</HashingAlgorithm>
                      <IssuerName>/C=US/O=Let's Encrypt/CN=R3</IssuerName>
                      <PublicKeySize>2048</PublicKeySize>
                      <SerialNumber>03AAD99323EE064071B6993725BDBECA3558</SerialNumber>
                      <SignatureAlgorithm>rsaEncryption</SignatureAlgorithm>
                      <SubjectName>/CN=roomkitplus.aldaviva.com</SubjectName>
                      <UsedFor>SIP, HTTPS server</UsedFor>
                      <Version>3</Version>
                      <notAfter>Aug 08 03:04:26 2023 GMT</notAfter>
                      <notBefore>May 10 03:04:27 2023 GMT</notBefore>
                    </Details>
                    <Details item="2" maxOccurrence="n">
                      <Fingerprint>a053375bfe84e8b748782c7cee15827a6af5a405</Fingerprint>
                      <HashingAlgorithm>sha256WithRSAEncryption</HashingAlgorithm>
                      <IssuerName>/C=US/O=Internet Security Research Group/CN=ISRG Root X1</IssuerName>
                      <PublicKeySize>2048</PublicKeySize>
                      <SerialNumber>912B084ACF0C18A753F6D62E25A75F5A</SerialNumber>
                      <SignatureAlgorithm>rsaEncryption</SignatureAlgorithm>
                      <SubjectName>/C=US/O=Let's Encrypt/CN=R3</SubjectName>
                      <UsedFor>SIP, HTTPS server</UsedFor>
                      <Version>3</Version>
                      <notAfter>Sep 15 16:00:00 2025 GMT</notAfter>
                      <notBefore>Sep 04 00:00:00 2020 GMT</notBefore>
                    </Details>
                    <Format>Text</Format>
                  </ServicesShowResult>
                </Command>
                """);

            XmlSerializer xmlSerializer = new(typeof(ServicesShowResult));
            XElement      xElement      = doc.Descendants("ServicesShowResult").First();

            XmlReader          xmlReader = xElement.CreateReader();
            ServicesShowResult actual    = (ServicesShowResult) xmlSerializer.Deserialize(xmlReader);
            Assert.Equal(actual.certificates.Count, 2);
            Assert.Equal(actual.certificates[0].fingerprint, "ce4eb947c2c17d58bbc08303bf278464beebf2af");
        }

    }

}