using FluentValidation;
using HMS.Application.Appointments;
using HMS.Application.Auth;
using HMS.Application.Billing;
using HMS.Application.Clinical;
using HMS.Application.Dashboard;
using HMS.Application.Doctors;
using HMS.Application.MasterData;
using HMS.Application.Patients;
using HMS.Application.Prescriptions;
using Microsoft.Extensions.DependencyInjection;

namespace HMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Scoped);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMasterDataService, MasterDataService>();
        services.AddScoped<IDoctorService, DoctorService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<ClinicalRevisionWriter>();
        services.AddScoped<IConsultationService, ConsultationService>();
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
