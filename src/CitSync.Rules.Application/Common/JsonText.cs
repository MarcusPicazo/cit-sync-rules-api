using System.Text.Json;

namespace CitSync.Rules.Application.Common;

internal static class JsonText
{
    /// <summary>Turns stored JSON text into a JsonElement so it is returned as JSON, not as an escaped string.</summary>
    public static JsonElement ToElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone(); // Clone: the element must outlive the disposed document
    }

    public static JsonElement? ToElementOrNull(string? json) => json is null ? null : ToElement(json);
}
