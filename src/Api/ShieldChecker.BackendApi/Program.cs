using Asp.Versioning;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Health;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────────────────────
// Use built-in ASP.NET Core logging (console + Application Insights in production).
// Structured HTTP request logging is provided by UseHttpLogging() below.
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestMethod
                    | HttpLoggingFields.RequestPath
                    | HttpLoggingFields.ResponseStatusCode
                    | HttpLoggingFields.Duration;
});

if (builder.Environment.IsProduction())
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// ── JWT / Microsoft Identity ─────────────────────────────────────────────────
// Validates tokens issued for the ShieldChecker-BackendApi app registration.
// Callers (the WebApp managed identity) must hold the 'WebApp.Access' AppRole.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

// ── EF Core ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ShieldCheckerContext>(options =>
    options.UseSqlServer(builder.Configuration.GetValue<string>("AzureSqlDatabase")));

// ── Key Vault ────────────────────────────────────────────────────────────────
if (builder.Environment.IsProduction())
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(builder.Configuration["KEYVAULT_URI"]!),
        new DefaultAzureCredential());
}

// ── Health Checks ─────────────────────────────────────────────────────────────
// SqlHealthCheck uses EF Core CanConnectAsync — no third-party package required.
builder.Services.AddHealthChecks()
    .AddCheck<SqlHealthCheck>("sqlserver", tags: ["ready"]);

// ── Caching ───────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// ── API Versioning ────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
});

// ── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi("v1");

builder.Services.AddControllers();

// ── Audit Service ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ShieldChecker.BackendApi.Services.AuditService>();

// ── Background Services ──────────────────────────────────────────────────────
builder.Services.AddHostedService<ShieldChecker.BackendApi.Services.AutoSchedulerService>();
builder.Services.AddHostedService<ShieldChecker.BackendApi.Services.MdeAlertDetectionService>();
builder.Services.AddHostedService<ShieldChecker.BackendApi.Services.JobTimeoutService>();

var app = builder.Build();

// Initialise / validate the database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ShieldCheckerContext>();
    DbInitializer.Initialize(context, app.Environment);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ── Health check endpoints ────────────────────────────────────────────────────
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description })
        });
        await ctx.Response.WriteAsync(result);
    }
}).AllowAnonymous();
app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new { status = report.Status.ToString() });
        await ctx.Response.WriteAsync(result);
    }
}).AllowAnonymous();

app.UseHttpLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
