using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HMS.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef`. Generating migrations does not connect to the database.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HmsDbContext>
{
    public HmsDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("HMS_DESIGN_CONNECTION")
                 ?? "Server=localhost;Port=3306;Database=hms_dev;User=hms_app;Password=design-time-only";
        var options = new DbContextOptionsBuilder<HmsDbContext>();
        DependencyInjection.ConfigureMySql(options, cs);
        return new HmsDbContext(options.Options);
    }
}
