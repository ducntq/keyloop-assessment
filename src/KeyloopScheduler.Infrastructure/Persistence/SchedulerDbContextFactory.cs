using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KeyloopScheduler.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can target this class
/// library directly, without depending on the API host project.
/// </summary>
public sealed class SchedulerDbContextFactory : IDesignTimeDbContextFactory<SchedulerDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=keyloop_scheduler;Username=postgres;Password=postgres";

    public SchedulerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SchedulerDbContext(options);
    }
}
