using System.Xml.Linq;

namespace csxapi;

internal static class Extensions {

    public static XElement? FindDescendentByElementNames(this XContainer ancestor, params string[] nestedChildrenNames) {
        string?   docNamespaceName = ancestor.Document?.Root?.Name.NamespaceName;
        XElement? ancestorEl       = null;

        foreach (string nestedChildName in nestedChildrenNames) {
            XName childName = docNamespaceName is null ? XName.Get(nestedChildName) : XName.Get(nestedChildName, docNamespaceName);
            if (ancestorEl is null) {
                ancestorEl = ancestor.Element(childName);
            } else {
                ancestorEl = ancestorEl.Element(childName);
                if (ancestorEl is null) {
                    return null;
                }
            }
        }

        return ancestorEl;
    }

    public static async Task<XDocument> ReadFromXmlAsync(this HttpContent content, LoadOptions xmlLoadOptions = LoadOptions.None, CancellationToken cancellationToken = default) {
        await using Stream contentStream = await content.ReadAsStreamAsync(cancellationToken);
        return await XDocument.LoadAsync(contentStream, xmlLoadOptions, cancellationToken);
    }

    // public static async Task<T?> readFromXmlAsync<T>(this HttpContent content, CancellationToken cancellationToken = default) {
    //     XmlSerializer      xmlSerializer = new(typeof(T));
    //     await using Stream inputStream   = await content.ReadAsStreamAsync(cancellationToken);
    //     return (T?) xmlSerializer.Deserialize(inputStream);
    // }

}