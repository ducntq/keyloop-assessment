using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KeyloopScheduler.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and seeds the development dataset. Invoked from
/// the API host during Development only.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(DbInitializer));

        logger?.LogInformation("Applying database migrations...");
        await context.Database.MigrateAsync(cancellationToken);

        await SeedAsync(context, logger, cancellationToken);
    }

    public static async Task SeedAsync(
        SchedulerDbContext context,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await SeedCatalogueAsync(context, logger, cancellationToken);
        await SeedCustomersAsync(context, logger, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCatalogueAsync(
        SchedulerDbContext context,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (await context.Dealerships.AnyAsync(cancellationToken))
        {
            logger?.LogInformation("Seed skipped: dealership data already present.");
            return;
        }

        var dealership = new Dealership(SeedData.DealershipId, SeedData.DealershipName);

        var bays = new[]
        {
            new ServiceBay(SeedData.GeneralLiftBayId, SeedData.DealershipId, "Bay 1 (General Lift)"),
            new ServiceBay(SeedData.AlignmentRackBayId, SeedData.DealershipId, "Bay 2 (Alignment Rack)"),
            new ServiceBay(SeedData.EvBayId, SeedData.DealershipId, "Bay 3 (EV Specialized Bay)")
        };

        var technicians = new[]
        {
            new Technician(SeedData.TechAId, SeedData.DealershipId, "Tech A", SeedData.TechACertifications),
            new Technician(SeedData.TechBId, SeedData.DealershipId, "Tech B", SeedData.TechBCertifications),
            new Technician(SeedData.TechCId, SeedData.DealershipId, "Tech C", SeedData.TechCCertifications),
            new Technician(SeedData.TechDId, SeedData.DealershipId, "Tech D", SeedData.TechDCertifications)
        };

        var serviceTypes = new[]
        {
            new ServiceType(
                SeedData.OilAndInspectionServiceTypeId,
                "Oil & Inspection",
                30,
                CertificationType.General),
            new ServiceType(
                SeedData.BrakePadReplacementServiceTypeId,
                "Brake Pad Replacement",
                60,
                CertificationType.Brakes),
            new ServiceType(
                SeedData.EvBatteryDiagnosticsServiceTypeId,
                "EV Battery Diagnostics",
                90,
                CertificationType.EvCertified)
        };

        context.Dealerships.Add(dealership);
        context.ServiceBays.AddRange(bays);
        context.Technicians.AddRange(technicians);
        context.ServiceTypes.AddRange(serviceTypes);

        logger?.LogInformation(
            "Seed complete: {BayCount} bays, {TechnicianCount} technicians, {ServiceTypeCount} service types.",
            bays.Length,
            technicians.Length,
            serviceTypes.Length);
    }

    private static async Task SeedCustomersAsync(
        SchedulerDbContext context,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (await context.Customers.AnyAsync(cancellationToken))
        {
            return;
        }

        var customers = new[]
        {
            new Customer(
                SeedData.CustomerAId,
                SeedData.DealershipId,
                SeedData.CustomerAFullName,
                SeedData.CustomerAEmail),
            new Customer(
                SeedData.CustomerBId,
                SeedData.DealershipId,
                SeedData.CustomerBFullName,
                SeedData.CustomerBEmail)
        };

        context.Customers.AddRange(customers);

        logger?.LogInformation("Seed complete: {CustomerCount} customers.", customers.Length);
    }
}
