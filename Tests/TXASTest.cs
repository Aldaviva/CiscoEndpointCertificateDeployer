using System.Collections.Generic;
using System.Xml.Linq;
using CiscoEndpointCertificateDeployer;
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

    }

}