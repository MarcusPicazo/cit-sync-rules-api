namespace CitSync.Rules.Application.Common;

/// <summary>
/// The school runs on Mexico City time. We store UTC and convert only when talking to people.
/// </summary>
public static class SchoolTimeZone
{
    public const string Id = "America/Mexico_City";

    // IANA ids also work on Windows since .NET 6 (ICU), so the same id runs locally and on Linux CI.
    public static readonly TimeZoneInfo Info = TimeZoneInfo.FindSystemTimeZoneById(Id);

    public static DateTimeOffset ToSchoolTime(DateTime utc) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), Info);
}
