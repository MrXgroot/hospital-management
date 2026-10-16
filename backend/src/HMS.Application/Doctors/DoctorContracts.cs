using FluentValidation;
using HMS.Domain.Common;

namespace HMS.Application.Doctors;

public sealed record DoctorSpecializationDto(long SpecializationId, string Name, bool IsPrimary);

public sealed record DoctorListItemDto(
    long Id, string DoctorCode, string FullName, long DepartmentId, string DepartmentName,
    string? PrimarySpecialization, decimal ConsultationFee, string Phone, bool IsActive);

public sealed record DoctorDto(
    long Id, string DoctorCode, string FirstName, string LastName, string FullName, Gender Gender,
    string Phone, string? Email, string Qualification, string RegistrationNumber,
    long DepartmentId, string DepartmentName, decimal ConsultationFee, short? YearsOfExperience, bool IsActive,
    IReadOnlyList<DoctorSpecializationDto> Specializations);

public sealed record SaveDoctorRequest(
    string FirstName, string LastName, Gender Gender, string Phone, string? Email, string Qualification,
    string RegistrationNumber, long DepartmentId, decimal ConsultationFee, short? YearsOfExperience,
    IReadOnlyList<long> SpecializationIds, long PrimarySpecializationId);

public sealed record SetDoctorStatusRequest(bool IsActive);

public sealed record ScheduleBlockDto(int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int SlotDurationMinutes);
public sealed record SaveScheduleRequest(IReadOnlyList<ScheduleBlockDto> Blocks);

public sealed record DoctorLeaveDto(long Id, DateOnly FromDate, DateOnly ToDate, string? Reason);
public sealed record CreateLeaveRequest(DateOnly FromDate, DateOnly ToDate, string? Reason);

public sealed record SlotDto(TimeOnly StartTime, TimeOnly EndTime, bool IsAvailable);

public sealed record AvailableDoctorDto(
    long Id, string FullName, string DepartmentName, string? PrimarySpecialization, string WorkingHours,
    int FreeSlotsRemaining, int WaitingCount);

public sealed record FutureAppointmentRef(long Id, DateOnly AppointmentDate, TimeOnly StartTime, string PatientName);

public sealed class SaveDoctorRequestValidator : AbstractValidator<SaveDoctorRequest>
{
    public SaveDoctorRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^\+?[0-9 -]{10,20}$").WithMessage("Enter a valid phone number.");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Qualification).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.ConsultationFee).GreaterThanOrEqualTo(0).LessThan(100_000_000);
        RuleFor(x => x.YearsOfExperience).InclusiveBetween((short)0, (short)70).When(x => x.YearsOfExperience.HasValue);
        RuleFor(x => x.SpecializationIds).NotEmpty().WithMessage("Select at least one specialization.");
        RuleFor(x => x).Must(x => x.SpecializationIds.Contains(x.PrimarySpecializationId))
            .WithName(nameof(SaveDoctorRequest.PrimarySpecializationId))
            .WithMessage("The primary specialization must be one of the selected specializations.");
    }
}

public sealed class SaveScheduleRequestValidator : AbstractValidator<SaveScheduleRequest>
{
    public SaveScheduleRequestValidator()
    {
        RuleForEach(x => x.Blocks).ChildRules(b =>
        {
            b.RuleFor(x => x.DayOfWeek).InclusiveBetween(0, 6);
            b.RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("End time must be after start time.");
            b.RuleFor(x => x.SlotDurationMinutes).InclusiveBetween(5, 120);
        });
    }
}

public sealed class CreateLeaveRequestValidator : AbstractValidator<CreateLeaveRequest>
{
    public CreateLeaveRequestValidator()
    {
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate).WithMessage("To date must be on or after from date.");
        RuleFor(x => x.Reason).MaximumLength(250);
    }
}
