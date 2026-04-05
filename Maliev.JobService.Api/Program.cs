using Maliev.JobService.Api.Consumers;
using Maliev.JobService.Application.Abstractions;
using Maliev.JobService.Api.Services;
using Maliev.JobService.Domain.Clients;
using Maliev.JobService.Infrastructure.Metrics;
using Maliev.JobService.Infrastructure.Persistence;
using Maliev.JobService.Infrastructure.Data.SeedData;
using Maliev.JobService.Infrastructure.Services;

// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Program.Log.StartingHost(bootstrapLogger, "Job Service");

    var builder = WebApplication.CreateBuilder(args);

    // --- Secrets & Configuration ---
    builder.AddGoogleSecretManagerVolume();

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults();
    builder.AddDefaultApiVersioning();
    builder.AddStandardMiddleware();
    builder.AddServiceMeters("job-meter");

    // Register DbContext
    builder.AddPostgresDbContext<JobDbContext>(connectionName: "JobDbContext");

    builder.AddStandardCache("job:");

    // MassTransit with RabbitMq
    builder.AddMassTransitWithRabbitMq(configure: x =>
    {
        x.AddConsumer<OrderPaidEventConsumer>();
        x.AddConsumer<OrderOutsourcingChangedConsumer>();
    });

    // JWT Authentication (also registers AddPermissionAuthorization internally)
    builder.AddJwtAuthentication();

    // IAM Registration
    builder.AddIAMServiceClient("job");
    builder.Services.AddIAMRegistration<JobIAMRegistrationService>("job");

    // Authenticated HTTP client for OrderService calls
    builder.AddAuthenticatedServiceClient<IOrderServiceClient, Maliev.JobService.Infrastructure.HttpClients.OrderServiceClient>("OrderService", sourceServiceName: "JobService");

    // --- API Configuration ---
    builder.AddStandardCors();

    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Job Service API",
            description: "Manages the physical production lifecycle of manufacturing jobs on the shop floor.");
    }

    builder.AddStandardRateLimiting();
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    builder.Services.AddSingleton<JobMetrics>();
    builder.Services.AddScoped<IJobService, JobService>();
    builder.Services.AddScoped<ISchedulingService, SchedulingService>();
    builder.Services.AddSingleton<ITimeEstimationService, TimeEstimationService>();

    builder.Services.AddControllers();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    // --- Database Migrations ---
    await app.MigrateDatabaseAsync<JobDbContext>();

    // --- Seed Jobs ---
    await app.SeedJobsAsync();

    // --- Middleware Pipeline ---
    app.UseStandardMiddleware();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseResponseCompression();
    app.UseRouting();
    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // --- Endpoints ---
    app.MapControllers();
    app.MapDefaultEndpoints(servicePrefix: "job");
    app.MapApiDocumentation(servicePrefix: "job");

    Program.Log.ServiceStarted(logger, "Job Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Program.Log.HostTerminated(bootstrapLogger, ex, "Job Service");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Main program class for the Job Service application.
/// </summary>
public partial class Program
{
    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
        public static partial void StartingHost(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
        public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
        public static partial void ServiceStarted(ILogger logger, string serviceName);
    }
}
