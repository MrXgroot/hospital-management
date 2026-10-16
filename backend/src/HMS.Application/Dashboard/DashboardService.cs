using HMS.Application.Common;
using HMS.Application.Doctors;
using HMS.Domain.Appointments;
using HMS.Domain.Billing;
using HMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Dashboard;

public sealed record TodayAppointmentsDto(int Total, IReadOnlyDictionary<string, int> ByStatus);

public sealed record WaitingPatientDto(
    long AppointmentId, short? QueueToken, long PatientId, string PatientName, string PatientMrn,
    long DoctorId, string DoctorName, TimeOnly StartTime, DateTime? CheckedInAt, long? ConsultationId);

public sealed record DashboardSummaryDto(
    int TotalPatients, TodayAppointmentsDto TodayAppointments, IReadOnlyList<AvailableDoctorDto> AvailableDoctors,
    IReadOnlyList<WaitingPatientDto> WaitingQueue, decimal TodayRevenue, decimal PendingPayments,
    int PendingInvoiceCount, string Currency, bool ScopedToDoctor);

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct);
}

public sealed class DashboardService(
    IAppDbContext db, IClock clock, ICurrentUser currentUser, IDoctorService doctors,
    Microsoft.Extensions.Options.IOptions<HospitalOptions> hospital) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var today = clock.Today;
        long? doctorScope = currentUser.Role == Role.Doctor ? currentUser.DoctorId : null;

        var totalPatients = await db.Patients.CountAsync(ct);

        var todayQuery = db.Appointments.AsNoTracking().Where(a => a.AppointmentDate == today && a.Status != AppointmentStatus.Cancelled);
        if (doctorScope is { } d) todayQuery = todayQuery.Where(a => a.DoctorId == d);
        var byStatus = await todayQuery.GroupBy(a => a.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);

        var waiting = await todayQuery.Where(a => a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InConsultation)
            .OrderBy(a => a.Status == AppointmentStatus.CheckedIn).ThenBy(a => a.QueueToken)
            .Select(a => new WaitingPatientDto(a.Id, a.QueueToken, a.PatientId, a.Patient.FirstName + " " + a.Patient.LastName, a.Patient.Mrn,
                a.DoctorId, "Dr. " + a.Doctor.FirstName + " " + a.Doctor.LastName, a.StartTime, a.CheckedInAt,
                db.Consultations.Where(c => c.AppointmentId == a.Id).Select(c => (long?)c.Id).FirstOrDefault()))
            .Take(50)
            .ToListAsync(ct);

        var (startUtc, endUtc) = clock.UtcRangeFor(today);
        var revenue = await db.Payments.Where(p => p.PaidAt >= startUtc && p.PaidAt < endUtc).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var pending = await db.Invoices.Where(i => i.Status == InvoiceStatus.Unpaid)
            .GroupBy(_ => 1).Select(g => new { Sum = g.Sum(i => i.TotalAmount - i.PaidAmount), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var available = await doctors.GetAvailableTodayAsync(ct);
        if (doctorScope is { } ds) available = available.Where(a => a.Id == ds).ToList();

        return new DashboardSummaryDto(
            totalPatients,
            new TodayAppointmentsDto(byStatus.Sum(s => s.Count), byStatus.ToDictionary(s => s.Status.ToString(), s => s.Count)),
            available, waiting, revenue, pending?.Sum ?? 0m, pending?.Count ?? 0, hospital.Value.Currency, doctorScope.HasValue);
    }
}
