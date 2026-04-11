using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using ShieldChecker.DataAccess;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// ── JWT / Microsoft Identity ─────────────────────────────────────────────────
// Validates tokens issued for the ShieldChecker-HostServiceApi app registration.
// Callers must hold the 'HostService.Access' AppRole.
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

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
