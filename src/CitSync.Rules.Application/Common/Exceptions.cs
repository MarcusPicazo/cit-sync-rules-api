namespace CitSync.Rules.Application.Common;

/// <summary>The requested entity does not exist (→ 404).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static NotFoundException For(string entity, object key) =>
        new($"{entity} '{key}' was not found.");
}

/// <summary>
/// Optimistic concurrency failure: someone else changed the row since the client read it (→ 409).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class ConcurrencyGuard
{
    /// <summary>
    /// If the client sent the version it last saw (If-Match), it must equal the current rowversion.
    /// Cheap early check; the database re-checks on UPDATE for races after this point.
    /// </summary>
    public static void EnsureVersion(byte[]? expected, byte[] current, string entity, object key)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (expected is not null && !expected.AsSpan().SequenceEqual(current))
        {
            throw new ConcurrencyConflictException(
                $"{entity} '{key}' was modified by someone else. Reload it and retry with the new version.");
        }
    }

    public static string ToToken(byte[] rowVersion) => Convert.ToBase64String(rowVersion);
}

/// <summary>The request clashes with current state, e.g. a duplicate name (→ 409).</summary>
public sealed class ConflictException : Exception
{
    public ConflictException()
    {
    }

    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
