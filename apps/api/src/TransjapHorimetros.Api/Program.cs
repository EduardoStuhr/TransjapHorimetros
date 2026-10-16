using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TransjapHorimetros.Api.Health;
using TransjapHorimetros.Api.Infrastructure;
using TransjapHorimetros.Api.Security;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Anomalies;
using TransjapHorimetros.Application.Dashboard;
using TransjapHorimetros.Application.Machines;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Application.QrCodes;
using TransjapHorimetros.Application.Security;
using TransjapHorimetros.Application.WorkSites;
using TransjapHorimetros.Domain.Rules;
using TransjapHorimetros.Domain.Security;
using TransjapHorimetros.Infrastructure;
using TransjapHorimetros.Infrastructure.Persistence;
using TransjapHorimetros.Infrastructure.Security;

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
builder.Services.AddScoped<IMachineQrCodeService, MachineQrCodeService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

var authenticationOptions = builder.Configuration
    .GetSection(AuthenticationOptions.SectionName)
    .Get<AuthenticationOptions>() ?? new AuthenticationOptions();
var jwtOptions = authenticationOptions.Jwt;

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)
    || string.IsNullOrWhiteSpace(jwtOptions.Audience))
{
    throw new InvalidOperationException(
        "Authentication:Jwt:Issuer e Authentication:Jwt:Audience devem ser configurados.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException(
        "Authentication:Jwt:SigningKey deve ser configurada por secret ou variavel de ambiente.");
}

if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException(
        "Authentication:Jwt:SigningKey deve possuir pelo menos 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(Policies.CanRecordReading, policy =>
        policy.RequireRole(AppRoles.Operator, AppRoles.Supervisor, AppRoles.Administrative, AppRoles.Manager, AppRoles.Admin));
    options.AddPolicy(Policies.CanResolveQr, policy =>
        policy.RequireRole(AppRoles.Operator, AppRoles.Supervisor, AppRoles.Administrative, AppRoles.Manager, AppRoles.Admin));
    options.AddPolicy(Policies.CanManageMachines, policy =>
        policy.RequireRole(AppRoles.Administrative, AppRoles.Manager, AppRoles.Admin));
    options.AddPolicy(Policies.CanManageQrCodes, policy =>
        policy.RequireRole(AppRoles.Administrative, AppRoles.Manager, AppRoles.Admin));
    options.AddPolicy(Policies.CanViewQrHistory, policy =>
        policy.RequireRole(AppRoles.Supervisor, AppRoles.Administrative, AppRoles.Manager, AppRoles.Admin));
    options.AddPolicy(Policies.CanManageWorkSites, policy =>
        policy.RequireRole(AppRoles.Administrative, AppRoles.Manager, AppRoles.Admin));
    options.AddPolicy(Policies.CanViewDashboard, policy =>
        policy.RequireRole(AppRoles.Supervisor, AppRoles.Administrative, AppRoles.Manager, AppRoles.Admin));
    options.AddPolicy(Policies.CanReviewAnomalies, policy =>
        policy.RequireRole(AppRoles.Supervisor, AppRoles.Administrative, AppRoles.Manager, AppRoles.Admin));
    options.AddPolicy(Policies.CanManageUsers, policy =>
        policy.RequireRole(AppRoles.Admin));
});

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
var loginPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:AuthLogin:PermitLimit") ?? 5;
var loginWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:AuthLogin:WindowSeconds") ?? 60;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    options.AddPolicy("auth-login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromSeconds(loginWindowSeconds),
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

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseCors("WebDevelopment");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // Mantido depois da autorização para que a fallback policy também proteja o Swagger.
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Transjap Horímetros API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();
app.MapHealthChecks("/health", HealthResponseWriter.Options()).AllowAnonymous();
app.MapHealthChecks("/health/ready", HealthResponseWriter.Options(registration =>
    registration.Tags.Contains("ready"))).AllowAnonymous();
app.MapHealthChecks("/health/live", HealthResponseWriter.Options(registration =>
    registration.Tags.Contains("live"))).AllowAnonymous();

if (builder.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup") ?? true)
{
    await app.Services.MigrateAndSeedAsync();
}

app.Logger.LogInformation("Transjap Horímetros API iniciada no ambiente {Environment}.", app.Environment.EnvironmentName);
app.Run();

public partial class Program;
