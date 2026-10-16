using FluentValidation;
using HMS.Domain.Appointments;

namespace HMS.Application.Appointments;

public sealed record AppointmentStatusHistoryDto(
    AppointmentStatus? FromStatus, AppointmentStatus ToStatus, DateOnly? OldDate, TimeOnly? OldStartTime,
    string? Note, DateTime ChangedAt, string? ChangedByName);

public sealed record AppointmentDto(
    long Id, long PatientId, string PatientMrn, string PatientName, string PatientPhone,
    long DoctorId, string DoctorName, string DepartmentName,
    DateOnly AppointmentDate, TimeOnly StartTime, TimeOnly EndTime,
    AppointmentStatus Status, AppointmentType AppointmentType, string? ReasonForVisit,
    short? QueueToken, DateTime? CheckedInAt, string? CancellationReason, short RescheduleCount,
    long? ConsultationId, long? InvoiceId, int RowVersion,
    IReadOnlyList<AppointmentStatusHistoryDto>? History = null);

public sealed record AppointmentQuery(
    DateOnly? Date, DateOnly? FromDate, DateOnly? ToDate, long? DoctorId, long? PatientId, AppointmentStatus? Status);

/// <summary>StartTime may be omitted for walk-ins: the next free slot today is used.</summary>
public sealed record CreateAppointmentRequest(
    long PatientId, long DoctorId, DateOnly AppointmentDate, TimeOnly? StartTime,
    AppointmentType AppointmentType = AppointmentType.Scheduled, string? ReasonForVisit = null,
    long? FollowUpOfConsultationId = null);

public sealed record RescheduleAppointmentRequest(DateOnly AppointmentDate, TimeOnly StartTime, int RowVersion);
public sealed record CancelAppointmentRequest(string Reason);

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.DoctorId).GreaterThan(0);
        RuleFor(x => x.AppointmentType).IsInEnum();
        RuleFor(x => x.StartTime).NotNull().When(x => x.AppointmentType != AppointmentType.WalkIn)
            .WithMessage("Select a time slot.");
        RuleFor(x => x.ReasonForVisit).MaximumLength(300);
    }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator() => RuleFor(x => x.RowVersion).GreaterThanOrEqualTo(0);
}

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
}
