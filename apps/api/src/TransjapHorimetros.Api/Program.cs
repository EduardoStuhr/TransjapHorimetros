using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using TransjapHorimetros.Api.Health;
using TransjapHorimetros.Api.Infrastructure;
using TransjapHorimetros.Application.Anomalies;
using TransjapHorimetros.Application.Configuration;
using TransjapHorimetros.Application.Dashboard;
using TransjapHorimetros.Application.Machines;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Application.WorkSites;
using TransjapHorimetros.Domain.Rules;
using TransjapHorimetros.Infrastructure;
using TransjapHorimetros.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);

builder.Services.AddSingleton(TimeProvider.System);

// Vincula todas as opções de regras operacionais a partir do appsettings.json (seção HourMeterRules)
var rulesOptions = builder.Configuration
    .GetSection(HourMeterRulesOptions.SectionName)
    .Get<HourMeterRulesOptions>() ?? new HourMeterRulesOptions();
builder.Services.AddSingleton(rulesOptions);
builder.Services.AddSingleton<HourMeterReadingPolicy>();
builder.Services.AddScoped<IMachineService, MachineService>();
builder.Services.AddScoped<IWorkSiteService, WorkSiteService>();
builder.Services.AddScoped<IReadingService, ReadingService>();
builder.Services.AddScoped<IAnomalyService, AnomalyService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation error",
            Detail = "Um ou mais campos enviados são inválidos.",
            Instance = context.HttpContext.Request.Path,
        };
        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        problemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problemDetails);
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Cast<string>()
    .ToArray();
builder.Services.AddCors(options =>
    options.AddPolicy("WebDevelopment", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 120;
var windowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 60;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Transjap Horímetros API",
        Version = "v1",
        Description = "API local para cadastro da frota, horímetros, obras, anomalias e indicadores reais.",
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), ["live", "ready"])
    .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Transjap Horímetros API v1");
        options.RoutePrefix = "swagger";
    });
}
else if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseCors("WebDevelopment");
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("api");
app.MapHealthChecks("/health", HealthResponseWriter.Options());
app.MapHealthChecks("/health/ready", HealthResponseWriter.Options(registration =>
    registration.Tags.Contains("ready")));
app.MapHealthChecks("/health/live", HealthResponseWriter.Options(registration =>
    registration.Tags.Contains("live")));

if (builder.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup") ?? true)
{
    await app.Services.MigrateAndSeedAsync();
}

app.Logger.LogInformation("Transjap Horímetros API iniciada no ambiente {Environment}.", app.Environment.EnvironmentName);
app.Run();

public partial class Program;
