using CitSync.Rules.Application.Rules;
using CitSync.Rules.Application.Rules.Engine;
using FluentAssertions;

namespace CitSync.Rules.UnitTests.Rules;

public class CapacityMathTests
{
    private static readonly DateTime Day = new(2030, 1, 21, 0, 0, 0, DateTimeKind.Utc);

    private static OccupiedSlot Slot(int fromHour, int toHour, int quantity) =>
        new(Day.AddHours(fromHour), Day.AddHours(toHour), quantity);

    private static int Peak(int fromHour, int toHour, params OccupiedSlot[] slots) =>
        CapacityMath.PeakConcurrentQuantity(slots, Day.AddHours(fromHour), Day.AddHours(toHour));

    [Fact]
    public void No_slots_means_zero()
    {
        Peak(9, 11).Should().Be(0);
    }

    [Fact]
    public void Back_to_back_slots_are_not_added_together()
    {
        // 9–10 and 10–11 both overlap 9–11 but never at the same time.
        Peak(9, 11, Slot(9, 10, 2), Slot(10, 11, 2)).Should().Be(2);
    }

    [Fact]
    public void Truly_concurrent_slots_add_up()
    {
        Peak(9, 12, Slot(9, 11, 2), Slot(10, 12, 3)).Should().Be(5);
    }

    [Fact]
    public void Peak_is_the_maximum_not_the_sum()
    {
        // 2 units 8–10, 3 units 9–11, 4 units 10:30–12 → at 10:30 only the last two coexist (7).
        var slots = new[] { Slot(8, 10, 2), Slot(9, 11, 3), new OccupiedSlot(Day.AddHours(10.5), Day.AddHours(12), 4) };

        CapacityMath.PeakConcurrentQuantity(slots, Day.AddHours(8), Day.AddHours(12)).Should().Be(7);
    }

    [Fact]
    public void Parts_outside_the_window_are_ignored()
    {
        Peak(10, 11, Slot(8, 10, 5), Slot(11, 12, 5)).Should().Be(0);
    }
}
