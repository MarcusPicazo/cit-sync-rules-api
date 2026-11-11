using FluentValidation;
using FluentValidation.Results;

namespace CitSync.Rules.Api.Http;

/// <summary>
/// Optimistic concurrency over HTTP: the client sends back the "version" it read, in If-Match.
/// Accepts the raw value, a quoted ETag ("...") or a weak one (W/"...").
/// </summary>
internal static class IfMatchHeader
{
    public const string Name = "If-Match";

    public static byte[]? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null; // header is optional: no check
        }

        var token = value.Trim();
        if (token.StartsWith("W/", StringComparison.Ordinal))
        {
            token = token[2..];
        }

        token = token.Trim('"');

        try
        {
            return Convert.FromBase64String(token);
        }
        catch (FormatException)
        {
            throw new ValidationException(
                [new ValidationFailure(Name, "If-Match must be the 'version' value returned by the API.")]);
        }
    }
}
