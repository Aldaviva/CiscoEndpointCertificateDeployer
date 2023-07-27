using System.Collections.Concurrent;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace CSxAPI;

internal static class Extensions {

    private static readonly ConcurrentDictionary<Type, XmlSerializer> XmlSerializers = new();

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
        await using Stream contentStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await XDocument.LoadAsync(contentStream, xmlLoadOptions, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<T> ReadFromXmlAsync<T>(this HttpContent content, CancellationToken cancellationToken = default) {
        XmlSerializer      xmlSerializer = XmlSerializers.GetOrAdd(typeof(T), type => new XmlSerializer(type));
        await using Stream inputStream   = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return (T) xmlSerializer.Deserialize(inputStream)!;
    }

    public static IDictionary<TKey, TValue> Compact<TKey, TValue>(this IDictionary<TKey, TValue?> dictionary) where TKey: notnull where TValue: class {
        return dictionary.Where(entry => entry.Value != null)
            .ToDictionary(entry => entry.Key, entry => entry.Value!);
    }

}