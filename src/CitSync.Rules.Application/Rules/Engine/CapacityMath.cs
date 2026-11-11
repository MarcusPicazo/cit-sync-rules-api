namespace CitSync.Rules.Application.Rules.Engine;

public static class CapacityMath
{
    /// <summary>
    /// Highest number of units in use at the same time inside [windowStart, windowEnd).
    /// Sweep line: every slot is a +quantity event at its start and a -quantity event at its end;
    /// walking the events in time order, the running total is the occupancy at that instant.
    /// O(n log n) for the sort; n is small (only reservations that overlap the window).
    /// </summary>
    /// <remarks>
    /// Why not just SUM the overlapping quantities? 9–10 (2 units) and 10–11 (2 units) both overlap
    /// a 9–11 window, but they never coexist: the real peak is 2, not 4.
    /// </remarks>
    public static int PeakConcurrentQuantity(
        IEnumerable<OccupiedSlot> slots, DateTime windowStart, DateTime windowEnd)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var events = new List<(DateTime At, int Delta)>();
        foreach (var slot in slots)
        {
            // Clip to the window: what happens outside it is irrelevant.
            var from = slot.StartUtc > windowStart ? slot.StartUtc : windowStart;
            var to = slot.EndUtc < windowEnd ? slot.EndUtc : windowEnd;
            if (from >= to)
            {
                continue;
            }

            events.Add((from, slot.Quantity));
            events.Add((to, -slot.Quantity));
        }

        // Same instant: process ends (negative) before starts, because intervals are half-open
        // [start, end): a reservation ending at 10:00 frees the units for one starting at 10:00.
        events.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Delta.CompareTo(b.Delta));

        int current = 0, peak = 0;
        foreach (var (_, delta) in events)
        {
            current += delta;
            peak = Math.Max(peak, current);
        }

        return peak;
    }
}
