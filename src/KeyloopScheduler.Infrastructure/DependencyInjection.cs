using KeyloopScheduler.Domain.Abstractions;
using KeyloopScheduler.Domain.Rules;
using KeyloopScheduler.Infrastructure.Persistence;
using KeyloopScheduler.Infrastructure.Persistence.Repositories;
using KeyloopScheduler.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KeyloopScheduler.Infrastructure;

/// <summary>
/// Composition root for the infrastructure layer. The API host calls
/// <see cref="AddInfrastructure"/> exactly once during startup.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<SchedulerDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorCodesToAdd: null)));

        // Abstracted clock: deterministic in tests, system clock in production.
        services.TryAddSingleton(TimeProvider.System);

        // Pluggable domain policy (open/closed): swap without touching the engine.
        services.TryAddSingleton<ITechnicianQualificationRule, CertificationQualificationRule>();

        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IResourceCatalogQuery, ResourceCatalogQuery>();
        services.AddScoped<IResourceAvailabilityQuery, ResourceAvailabilityQuery>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentBookingService, AppointmentBookingService>();

        return services;
    }
}
