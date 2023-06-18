// ReSharper disable InconsistentNaming - Extension methods

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace CiscoEndpointDocumentationApiExtractor;

public static class Extensions {

    public static string? EmptyToNull(this string? original) {
        return string.IsNullOrEmpty(original) ? null : original;
    }

    public static string? EmptyOrWhitespaceToNull(this string? original) {
        return string.IsNullOrWhiteSpace(original) ? null : original;
    }

    public static XElement? findDescendentByElementNames(this XContainer ancestor, params string[] nestedChildrenNames) {
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

    public static async Task<XDocument> readFromXmlAsync(this HttpContent content, LoadOptions xmlLoadOptions = LoadOptions.None, CancellationToken cancellationToken = default) {
        await using Stream contentStream = await content.ReadAsStreamAsync(cancellationToken);
        return await XDocument.LoadAsync(contentStream, xmlLoadOptions, cancellationToken);
    }

    public static async Task<T?> readFromXmlAsync<T>(this HttpContent content, CancellationToken cancellationToken = default) {
        XmlSerializer      xmlSerializer = new(typeof(T));
        await using Stream inputStream   = await content.ReadAsStreamAsync(cancellationToken);
        return (T?) xmlSerializer.Deserialize(inputStream);
    }

    [return: NotNullIfNotNull(nameof(stringWithNewLines))]
    public static string? NewLinesToParagraphs(this string? stringWithNewLines) {
        return string.IsNullOrEmpty(stringWithNewLines) ? stringWithNewLines : string.Join(null, Regex.Split(stringWithNewLines, @"\r?\n").Select(s => $"<para>{SecurityElement.Escape(s)}</para>"));

    }

    [return: NotNullIfNotNull(nameof(input))]
    public static string? ToLowerFirstLetter(this string? input) {
        return string.IsNullOrEmpty(input) ? input : char.ToLowerInvariant(input[0]) + input[1..];
    }

    [return: NotNullIfNotNull(nameof(input))]
    public static string? ToUpperFirstLetter(this string? input) {
        return string.IsNullOrEmpty(input) ? input : char.ToUpperInvariant(input[0]) + input[1..];
    }

}