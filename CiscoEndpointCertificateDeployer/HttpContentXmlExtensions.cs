using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CiscoEndpointCertificateDeployer {

    public static class HttpContentXmlExtensions {

        public static async Task<XDocument> ReadFromXmlAsync(this HttpContent content, LoadOptions xmlLoadOptions = LoadOptions.None, CancellationToken cancellationToken = default) {
            await using Stream contentStream = await content.ReadAsStreamAsync(cancellationToken);
            return await XDocument.LoadAsync(contentStream, xmlLoadOptions, cancellationToken);
        }

    }

}