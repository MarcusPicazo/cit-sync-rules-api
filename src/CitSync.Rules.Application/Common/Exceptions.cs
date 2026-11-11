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
