using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.EntityFrameworkCore;
using ShieldChecker.WebApp.Models.Db;
using Azure.Identity;
using ShieldChecker.WebApp.Services;

namespace ShieldChecker.WebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            if (builder.Environment.IsProduction())
            {
                builder.Services.AddApplicationInsightsTelemetry();
            }
            // Add services to the container.
            builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

            builder.Services.AddAuthorization(options =>
            {
                // By default, all incoming requests will be authorized according to the default policy.
                options.FallbackPolicy = options.DefaultPolicy;

                // ── In-app RBAC ───────────────────────────────────────────────────
                // These policies map to AppRoles configured in the ShieldChecker WebApp
                // app registration in Entra ID.  Assign roles to users/groups in the
                // Enterprise Applications blade.
                //
                //   ShieldChecker.Admin    – full control (settings, delete, approve)
                //   ShieldChecker.Operator – run/rerun/cancel tests
                //   ShieldChecker.Viewer   – read-only access
                options.AddPolicy("RequireAdmin", policy =>
                    policy.RequireRole("ShieldChecker.Admin"));
                options.AddPolicy("RequireOperator", policy =>
                    policy.RequireRole("ShieldChecker.Admin", "ShieldChecker.Operator"));
                options.AddPolicy("RequireViewer", policy =>
                    policy.RequireRole("ShieldChecker.Admin", "ShieldChecker.Operator", "ShieldChecker.Viewer"));
            });
            builder.Services.AddRazorPages()
                .AddMicrosoftIdentityUI();

            // EF Core is no longer used directly in the WebApp – all data access goes
            // through the BackendApi. Keeping DB context only for development/migration tooling.
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddDbContext<ShieldCheckerContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetValue<string>("AzureSqlDatabase")));
                builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            }

            if (builder.Environment.IsProduction())
            {
                
                builder.Configuration.AddAzureKeyVault(new Uri(builder.Configuration["KEYVAULT_URI"]),new DefaultAzureCredential());
            }
            builder.Services.AddServerSideBlazor();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHttpClient();

            // ── Backend API typed HTTP client ─────────────────────────────────────
            // The BackendApiService calls the internal ShieldChecker Backend API container
            // using the WebApp's managed identity (WebApp.Access AppRole).
            builder.Services.AddHttpClient<IBackendApiService, BackendApiService>();

            builder.Services.AddScoped<IAzureFunctionService, AzureFunctionService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            else
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapRazorPages();
            app.MapBlazorHub();
            app.MapControllers();

            app.Run();
        }
    }
}
