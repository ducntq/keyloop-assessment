using System.Reflection;
using KeyloopScheduler.Api.Filters;
using KeyloopScheduler.Api.Middleware;
using KeyloopScheduler.Infrastructure;
using KeyloopScheduler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.AddControllers(options => options.Filters.Add<GlobalExceptionFilter>());
builder.Services.AddProblemDetails();

// Uniform error contract: model-binding failures also emit RFC 7807 problem+json.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://httpstatuses.io/400",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new JsonResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentType = "application/problem+json"
        };
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Keyloop Unified Service Scheduler",
        Version = "v1",
        Description =
            "Resource-constrained automotive appointment scheduling engine. " +
            "Every booking reserves both a service bay and a certified technician."
    });

    var xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFileName);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Keyloop Scheduler v1");
    });

    // Apply migrations and seed the development dataset.
    await DbInitializer.InitializeAsync(app.Services);
}
else
{
    app.UseExceptionHandler();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithName("Health");

app.Run();

/// <summary>
/// Exposed so the integration test host (<c>WebApplicationFactory&lt;Program&gt;</c>)
/// can reference the entry-point assembly.
/// </summary>
public partial class Program;
