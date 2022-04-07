// ReSharper disable InconsistentNaming - Extension methods

namespace CiscoEndpointDocumentationApiExtractor;

public static class Extensions {

    public static string? EmptyToNull(this string? original) {
        return string.IsNullOrEmpty(original) ? null : original;
    }

    public static string? EmptyOrWhitespaceToNull(this string? original) {
        return string.IsNullOrWhiteSpace(original) ? null : original;
    }

}