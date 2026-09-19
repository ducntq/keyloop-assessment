using KeyloopScheduler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace KeyloopScheduler.Tests.Infrastructure;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL 16 container. The
/// application's <see cref="SchedulerDbContext"/> registration is replaced with
/// one pointed at the container, so startup migration + seeding run for real.
/// </summary>
public sealed class SchedulerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("keyloop_scheduler")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _container.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Program.cs reads the connection string from IConfiguration before the test
            // host can override configuration, so the registration itself must be swapped.
            services.RemoveAll<DbContextOptions<SchedulerDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<SchedulerDbContext>();

            services.AddDbContext<SchedulerDbContext>(options =>
                options.UseNpgsql(ConnectionString));
        });
    }

    /// <summary>Runs a unit of work against the container's database.</summary>
    public async Task<TResult> WithDbContextAsync<TResult>(Func<SchedulerDbContext, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        return await action(context);
    }

    public Task WithDbContextAsync(Func<SchedulerDbContext, Task> action) =>
        WithDbContextAsync<object?>(async context =>
        {
            await action(context);
            return null;
        });
}
