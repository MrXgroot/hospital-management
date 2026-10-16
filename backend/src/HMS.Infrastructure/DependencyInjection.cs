using HMS.Application.Common;
using HMS.Infrastructure.Persistence;
using HMS.Infrastructure.Persistence.Seed;
using HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HMS.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Pinned server version: AutoDetect would open a connection at startup.</summary>
    public static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 36));

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HospitalOptions>(configuration.GetSection(HospitalOptions.Section));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));

        var connectionString = configuration.GetConnectionString("HmsDb")
                               ?? throw new InvalidOperationException("Connection string 'HmsDb' is not configured.");

        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<HmsDbContext>((sp, options) =>
        {
            ConfigureMySql(options, connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
            // Doctors are never soft-deleted, and patients only when they have no clinical records,
            // so the filtered required-navigation case EF warns about doesn't arise.
            options.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<HmsDbContext>());

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ISequenceGenerator, SequenceGenerator>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<DbSeeder>();
        return services;
    }

    public static void ConfigureMySql(DbContextOptionsBuilder options, string connectionString) =>
        options.UseMySql(connectionString, ServerVersion, my =>
            {
                my.MigrationsAssembly(typeof(HmsDbContext).Assembly.FullName);
                my.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
            })
            .UseSnakeCaseNamingConvention();
}
