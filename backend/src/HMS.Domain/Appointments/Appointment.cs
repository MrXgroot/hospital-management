using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Patients;

namespace HMS.Domain.Appointments;

public enum AppointmentStatus { Booked, Confirmed, CheckedIn, InConsultation, Completed, Cancelled, NoShow }

public enum AppointmentType { Scheduled, WalkIn, FollowUp }

public class Appointment : AuditableEntity, IVersioned
{
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
    {
        [AppointmentStatus.Booked] = [AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],
        [AppointmentStatus.Confirmed] = [AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],
        [AppointmentStatus.CheckedIn] = [AppointmentStatus.InConsultation, AppointmentStatus.Cancelled],
        [AppointmentStatus.InConsultation] = [AppointmentStatus.Completed],
        [AppointmentStatus.Completed] = [],
        [AppointmentStatus.Cancelled] = [],
        [AppointmentStatus.NoShow] = [],
    };

    /// <summary>Statuses that no longer hold the slot (mirrors the active_slot generated column).</summary>
    public static readonly AppointmentStatus[] SlotReleasingStatuses = [AppointmentStatus.Cancelled, AppointmentStatus.NoShow];

    public long PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public long DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    public AppointmentType AppointmentType { get; set; } = AppointmentType.Scheduled;
    public string? ReasonForVisit { get; set; }
    public short? QueueToken { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public short RescheduleCount { get; set; }
    public long? FollowUpOfConsultationId { get; set; }
    /// <summary>DB-generated: 1 while the appointment holds its slot, NULL once cancelled or no-show.</summary>
    public byte? ActiveSlot { get; private set; }
    public int RowVersion { get; set; }

    public List<AppointmentStatusHistory> History { get; set; } = [];

    public bool HoldsSlot => !SlotReleasingStatuses.Contains(Status);

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to) =>
        AllowedTransitions[from].Contains(to);

    /// <summary>Changes status and records history. Throws INVALID_STATUS_TRANSITION if not allowed.</summary>
    public void TransitionTo(AppointmentStatus to, DateTime utcNow, long? userId, string? note = null)
    {
        if (!CanTransition(Status, to))
            throw new BusinessRuleException("INVALID_STATUS_TRANSITION",
                $"An appointment cannot move from {Status} to {to}.");

        History.Add(new AppointmentStatusHistory
        {
            FromStatus = Status, ToStatus = to, Note = note, ChangedAt = utcNow, ChangedBy = userId,
        });
        Status = to;
    }

    public void Cancel(string reason, DateTime utcNow, long? userId)
    {
        TransitionTo(AppointmentStatus.Cancelled, utcNow, userId, reason);
        CancelledAt = utcNow;
        CancellationReason = reason;
    }

    public void Reschedule(DateOnly date, TimeOnly start, TimeOnly end, DateTime utcNow, long? userId)
    {
        if (Status is not (AppointmentStatus.Booked or AppointmentStatus.Confirmed))
            throw new BusinessRuleException("INVALID_STATUS_TRANSITION",
                $"Only booked or confirmed appointments can be rescheduled (current: {Status}).");

        History.Add(new AppointmentStatusHistory
        {
            FromStatus = Status, ToStatus = Status, OldDate = AppointmentDate, OldStartTime = StartTime,
            Note = "Rescheduled", ChangedAt = utcNow, ChangedBy = userId,
        });
        AppointmentDate = date;
        StartTime = start;
        EndTime = end;
        RescheduleCount++;
    }
}

public class AppointmentStatusHistory : Entity
{
    public long AppointmentId { get; set; }
    public AppointmentStatus? FromStatus { get; set; }
    public AppointmentStatus ToStatus { get; set; }
    public DateOnly? OldDate { get; set; }
    public TimeOnly? OldStartTime { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
    public long? ChangedBy { get; set; }
}
