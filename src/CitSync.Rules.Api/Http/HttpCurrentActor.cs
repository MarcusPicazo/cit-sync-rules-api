using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Domain.Auditing;

namespace CitSync.Rules.Api.Http;

/// <summary>
/// Reads the caller's identity from the X-Actor header.
/// This is NOT authentication: anyone can send any value. It is a seam so the audit trail and
/// the approval workflow work today; it will be replaced by the user from the JWT (claims)
/// without touching the services or the audit interceptor.
/// </summary>
internal sealed class HttpCurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    public const string HeaderName = "X-Actor";
    public const string Anonymous = "anonymous";
    public const string System = "system";

    public string Name
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return System; // background work, migrations, tests that use the DbContext directly
            }

            var value = httpContext.Request.Headers[HeaderName].ToString().Trim();
            if (value.Length == 0)
            {
                return Anonymous;
            }

            return value.Length > AuditEntry.ActorMaxLength ? value[..AuditEntry.ActorMaxLength] : value;
        }
    }
}
