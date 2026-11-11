using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Rules;
using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Application.Rules.Evaluators;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using FluentAssertions;
using NSubstitute;
using static CitSync.Rules.UnitTests.Rules.RuleTestData;

namespace CitSync.Rules.UnitTests.Rules;

public class EvaluatorTests
{
    private static async Task<IReadOnlyList<RuleViolation>> Run(IRuleEvaluator evaluator, string json, RuleContext context)
    {
        evaluator.ValidateParameters(json).Should().BeEmpty("the test parameters must be valid");
        return await evaluator.EvaluateAsync(json, context, CancellationToken.None);
    }

    // ---------- parameter validation (shared base class) ----------

    [Fact]
    public void Unknown_parameter_name_is_reported_instead_of_ignored()
    {
        new MaxDurationEvaluator().ValidateParameters("""{"maxMinute":120}""")
            .Should().ContainSingle().Which.Should().Contain("maxMinute");
    }

    [Fact]
    public void Semantic_parameter_errors_are_reported()
    {
        new OpeningHoursEvaluator()
            .ValidateParameters("""{"schedule":[{"days":["Monday"],"open":"20:00","close":"07:00"}]}""")
            .Should().ContainSingle().Which.Should().Contain("'open' must be before 'close'");
    }

    // ---------- OpeningHours ----------

    private const string WeekdaysSevenToEight =
        """{"schedule":[{"days":["Monday","Tuesday","Wednesday","Thursday","Friday"],"open":"07:00","close":"20:00"}]}""";

    [Fact]
    public async Task OpeningHours_inside_the_window_is_allowed()
    {
        var result = await Run(new OpeningHoursEvaluator(), WeekdaysSevenToEight, Context(Monday(9), Monday(11)));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OpeningHours_uses_school_time_not_utc()
    {
        // 19:00–21:00 in Mexico City is 01:00–03:00 UTC of the NEXT day: must be judged locally.
        var result = await Run(new OpeningHoursEvaluator(), WeekdaysSevenToEight, Context(Monday(19), Monday(21)));

        result.Should().ContainSingle().Which.Code.Should().Be("OutsideOpeningHours");
    }

    [Fact]
    public async Task OpeningHours_on_a_day_without_windows_is_closed()
    {
        var sunday = Monday(9).AddDays(-1);

        var result = await Run(new OpeningHoursEvaluator(), WeekdaysSevenToEight, Context(sunday, sunday.AddHours(1)));

        result.Should().ContainSingle().Which.Code.Should().Be("ClosedDay");
    }

    [Fact]
    public async Task OpeningHours_rejects_overnight_reservations()
    {
        var result = await Run(new OpeningHoursEvaluator(), WeekdaysSevenToEight, Context(Monday(18), Monday(9).AddDays(1)));

        result.Should().ContainSingle().Which.Code.Should().Be("SpansMultipleDays");
    }

    // ---------- MaxDuration ----------

    [Fact]
    public async Task MaxDuration_over_the_limit_is_a_violation()
    {
        var result = await Run(new MaxDurationEvaluator(), """{"maxMinutes":120}""", Context(Monday(9), Monday(12)));

        result.Should().ContainSingle().Which.Code.Should().Be("MaxDurationExceeded");
    }

    [Fact]
    public async Task MaxDuration_only_applies_to_the_listed_roles()
    {
        var json = """{"maxMinutes":60,"roles":["Alumno"]}""";

        var alumno = await Run(new MaxDurationEvaluator(), json, Context(Monday(9), Monday(11)));
        var profesor = await Run(new MaxDurationEvaluator(), json, Context(Monday(9), Monday(11), RequesterRole.Profesor));

        alumno.Should().ContainSingle();
        profesor.Should().BeEmpty();
    }

    // ---------- RoleRestriction ----------

    [Fact]
    public async Task RoleRestriction_blocks_denied_roles()
    {
        var result = await Run(new RoleRestrictionEvaluator(), """{"deniedRoles":["Alumno"]}""", Context(Monday(9), Monday(10)));

        result.Should().ContainSingle().Which.Code.Should().Be("RoleNotAllowed");
    }

    [Fact]
    public async Task RoleRestriction_with_categories_ignores_other_categories()
    {
        var json = """{"deniedRoles":["Alumno"],"categories":["Equipment"]}""";

        var result = await Run(new RoleRestrictionEvaluator(), json, Context(Monday(9), Monday(10), resource: Lab()));

        result.Should().BeEmpty("the rule only covers Equipment, this is a Lab");
    }

    // ---------- NoOverlap ----------

    [Fact]
    public async Task NoOverlap_rejects_when_peak_plus_request_exceeds_capacity()
    {
        var repository = Substitute.For<IReservationRepository>();
        var context = Context(Monday(9), Monday(11), quantity: 2, resource: Lab(capacity: 5));
        repository.GetOccupiedSlotsAsync(
                context.Resource.Id, context.StartUtc, context.EndUtc, true, null, Arg.Any<CancellationToken>())
            .Returns([new OccupiedSlot(context.StartUtc, context.EndUtc, 4)]);

        var result = await Run(new NoOverlapEvaluator(repository), "{}", context);

        result.Should().ContainSingle().Which.Message.Should().Contain("1 of 5");
    }

    [Fact]
    public async Task NoOverlap_passes_countPending_to_the_query()
    {
        var repository = Substitute.For<IReservationRepository>();
        repository.GetOccupiedSlotsAsync(default, default, default, default, default, default)
            .ReturnsForAnyArgs([]);

        var result = await Run(new NoOverlapEvaluator(repository), """{"countPending":false}""", Context(Monday(9), Monday(10)));

        result.Should().BeEmpty();
        await repository.Received(1).GetOccupiedSlotsAsync(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), false, null, Arg.Any<CancellationToken>());
    }

    // ---------- LeadTime (TODO Marcus) ----------

    [Fact(Skip = "TODO(Marcus): remove after implementing LeadTimeEvaluator")]
    public async Task LeadTime_with_too_little_notice_is_a_violation()
    {
        // Now = Monday 2030-01-14 06:00 local; booking Monday 2030-01-14 09:00 local = 3 hours of notice.
        var context = Context(new DateTime(2030, 1, 14, 9, 0, 0), new DateTime(2030, 1, 14, 10, 0, 0));

        var result = await Run(new LeadTimeEvaluator(), """{"minHours":24}""", context);

        result.Should().ContainSingle().Which.Code.Should().Be("LeadTimeNotMet");
    }

    [Fact(Skip = "TODO(Marcus): remove after implementing LeadTimeEvaluator")]
    public async Task LeadTime_with_enough_notice_is_allowed()
    {
        var result = await Run(new LeadTimeEvaluator(), """{"minHours":24}""", Context(Monday(9), Monday(10)));

        result.Should().BeEmpty("a week of notice is more than 24 hours");
    }

    [Fact(Skip = "TODO(Marcus): remove after implementing LeadTimeEvaluator")]
    public async Task LeadTime_start_in_the_past_is_a_violation()
    {
        var context = Context(new DateTime(2030, 1, 13, 9, 0, 0), new DateTime(2030, 1, 13, 10, 0, 0));

        var result = await Run(new LeadTimeEvaluator(), """{"minHours":0}""", context);

        result.Should().ContainSingle().Which.Code.Should().Be("LeadTimeNotMet");
    }

    // ---------- BlackoutDates (TODO Marcus) ----------

    private const string WinterBreak =
        """{"periods":[{"from":"2030-12-20","to":"2031-01-06","reason":"Vacaciones de invierno"}]}""";

    [Fact(Skip = "TODO(Marcus): remove after implementing BlackoutDatesEvaluator")]
    public async Task BlackoutDates_inside_a_period_is_a_violation_with_its_reason()
    {
        var day = new DateTime(2030, 12, 23, 9, 0, 0);

        var result = await Run(new BlackoutDatesEvaluator(), WinterBreak, Context(day, day.AddHours(1)));

        var violation = result.Should().ContainSingle().Subject;
        violation.Code.Should().Be("BlackoutDate");
        violation.Message.Should().Contain("Vacaciones de invierno");
    }

    [Fact(Skip = "TODO(Marcus): remove after implementing BlackoutDatesEvaluator")]
    public async Task BlackoutDates_bounds_are_inclusive()
    {
        var lastDay = new DateTime(2031, 1, 6, 9, 0, 0);

        var result = await Run(new BlackoutDatesEvaluator(), WinterBreak, Context(lastDay, lastDay.AddHours(1)));

        result.Should().ContainSingle();
    }

    [Fact(Skip = "TODO(Marcus): remove after implementing BlackoutDatesEvaluator")]
    public async Task BlackoutDates_uses_the_school_local_date()
    {
        // 2030-12-19 18:30 local = 2030-12-20 00:30 UTC: in UTC it is already inside the break,
        // but at school it is still the 19th → allowed.
        var evening = new DateTime(2030, 12, 19, 18, 30, 0);

        var result = await Run(new BlackoutDatesEvaluator(), WinterBreak, Context(evening, evening.AddHours(1)));

        result.Should().BeEmpty();
    }
}
