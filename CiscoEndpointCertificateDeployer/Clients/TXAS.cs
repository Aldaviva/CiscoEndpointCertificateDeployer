using System.Net.Http.Headers;
using System.Xml.Linq;

namespace CiscoEndpointCertificateDeployer.Clients;

/// <summary>
/// Documentation:
///     TC7: https://www.cisco.com/c/dam/en/us/td/docs/telepresence/endpoint/codec-c-series/tc7/api-reference-guide/codec-c60-c40-api-reference-guide-tc73.pdf
/// </summary>
public class TXAS {

    private static readonly XAttribute COMMAND = new("command", "True");

    public static XDocument createCommand(string command, IDictionary<string, string>? parameters = null) {
        return createCommand(command.Split(' '), parameters);
    }

    public static XDocument createCommand(IEnumerable<string> commandParts, IDictionary<string, string>? parameters = null) {
        commandParts = new[] { "Command" }.Concat(commandParts.SkipWhile(s =>
            s.Equals("Command", StringComparison.InvariantCultureIgnoreCase) || s.Equals("xCommand", StringComparison.InvariantCultureIgnoreCase)));

        XContainer commandEl = commandParts.Aggregate((XContainer) new XDocument(), (parentEl, command) => {
            XElement childEl = new(command);
            parentEl.Add(childEl);
            return childEl;
        });

        commandEl.Add(new object[] { COMMAND }.Concat(parameters?.Select(pair => new XElement(pair.Key, pair.Value)) ?? Enumerable.Empty<XElement>()).ToArray());

        return commandEl.Document!;
    }

}

internal class TxasRequestContent: StreamContent {

    public TxasRequestContent(XDocument requestDoc): this(requestDoc, new MemoryStream()) { }

    private TxasRequestContent(XDocument requestDoc, Stream stream): base(stream) {
        Headers.ContentType = new MediaTypeHeaderValue("text/xml");

        requestDoc.Save(stream);
        stream.Seek(0, SeekOrigin.Begin);

        Console.WriteLine("request:\n" + requestDoc.ToString(SaveOptions.None));
    }

}

public enum ServicePurpose {

    _8021X,
    AUDIT,
    HTTPS,

    /// <summary>Not available on TC</summary>
    PAIRING,
    SIP,

    /// <summary>Not available on TC</summary>
    HTTP_CLIENT,

    /// <summary>Not available on TC</summary>
    WEBEX_IDENTITY

}

public static class ServicePurposeMethods {

    public static string txasName(this ServicePurpose servicePurpose) => servicePurpose switch {
        ServicePurpose._8021X         => "802.1X",
        ServicePurpose.AUDIT          => "Audit",
        ServicePurpose.HTTPS          => "HTTPS",
        ServicePurpose.PAIRING        => "Pairing",
        ServicePurpose.SIP            => "SIP",
        ServicePurpose.HTTP_CLIENT    => "HttpClient",
        ServicePurpose.WEBEX_IDENTITY => "WebexIdentity",
        _                             => throw new ArgumentOutOfRangeException(nameof(servicePurpose), servicePurpose, null)
    };

    public static string vegaNameTc(this ServicePurpose servicePurpose) => servicePurpose switch {
        ServicePurpose._8021X         => "802.1X",
        ServicePurpose.AUDIT          => "Audit log",
        ServicePurpose.HTTPS          => "HTTPS Server",
        ServicePurpose.PAIRING        => throw new ArgumentException("Pairing certificates are not available in TC"),
        ServicePurpose.SIP            => "SIP",
        ServicePurpose.HTTP_CLIENT    => throw new ArgumentException("Mutual TLS authentication certificates are not available in TC"),
        ServicePurpose.WEBEX_IDENTITY => throw new ArgumentException("Webex cloud end-to-end encryption certificates are not available in TC"),
        _                             => throw new ArgumentOutOfRangeException(nameof(servicePurpose), servicePurpose, null)
    };

    public static string vegaIdTc(this ServicePurpose servicePurpose) => servicePurpose switch {
        ServicePurpose._8021X         => "802.1X",
        ServicePurpose.AUDIT          => "auditlog",
        ServicePurpose.HTTPS          => "https_server",
        ServicePurpose.PAIRING        => throw new ArgumentException("Pairing certificates are not available in TC"),
        ServicePurpose.SIP            => "sip",
        ServicePurpose.HTTP_CLIENT    => throw new ArgumentException("Mutual TLS authentication certificates are not available in TC"),
        ServicePurpose.WEBEX_IDENTITY => throw new ArgumentException("Webex cloud end-to-end encryption certificates are not available in TC"),
        _                             => throw new ArgumentOutOfRangeException(nameof(servicePurpose), servicePurpose, null)
    };

}