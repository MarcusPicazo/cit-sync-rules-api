namespace CitSync.Rules.Domain.Common;

/// <summary>
/// A business invariant was broken (for example, approving a reservation that was already rejected).
/// The API turns it into a 409 Conflict ProblemDetails.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
