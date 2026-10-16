namespace HMS.Domain.Doctors;

public readonly record struct TimeSlot(TimeOnly Start, TimeOnly End);

public static class SlotGenerator
{
    /// <summary>Splits the active schedule blocks for the date's weekday into slots. Empty if on leave.</summary>
    public static IReadOnlyList<TimeSlot> Generate(
        DateOnly date, IEnumerable<DoctorSchedule> schedules, IEnumerable<DoctorLeave> leaves)
    {
        if (leaves.Any(l => l.Covers(date)))
            return [];

        var day = (byte)date.DayOfWeek;
        var slots = new List<TimeSlot>();
        foreach (var block in schedules.Where(s => s.IsActive && s.DayOfWeek == day).OrderBy(s => s.StartTime))
        {
            var step = TimeSpan.FromMinutes(block.SlotDurationMinutes);
            var start = block.StartTime;
            // TimeOnly wraps at midnight, so also stop if adding the step wrapped around.
            while (start.Add(step) <= block.EndTime && start.Add(step) > start)
            {
                slots.Add(new TimeSlot(start, start.Add(step)));
                start = start.Add(step);
            }
        }
        return slots;
    }

    /// <summary>Returns one message per overlapping block pair; empty when the schedule is valid.</summary>
    public static IReadOnlyList<string> FindOverlaps(IEnumerable<DoctorSchedule> blocks)
    {
        var errors = new List<string>();
        foreach (var day in blocks.GroupBy(b => b.DayOfWeek))
        {
            var ordered = day.OrderBy(b => b.StartTime).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].StartTime < ordered[i - 1].EndTime)
                    errors.Add($"Blocks overlap on {(DayOfWeek)day.Key}: " +
                               $"{ordered[i - 1].StartTime:HH\\:mm}-{ordered[i - 1].EndTime:HH\\:mm} and " +
                               $"{ordered[i].StartTime:HH\\:mm}-{ordered[i].EndTime:HH\\:mm}.");
            }
        }
        return errors;
    }
}
