namespace KeyloopScheduler.Tests.Infrastructure;

/// <summary>
/// One shared PostgreSQL container for the read-mostly integration contract tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<SchedulerApiFactory>
{
    public const string Name = "Integration";
}

/// <summary>
/// Dedicated container for the race-condition suite, which mutates the resource
/// catalogue down to a single bay and a single qualified technician.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ConcurrencyCollection : ICollectionFixture<SchedulerApiFactory>
{
    public const string Name = "Concurrency";
}
