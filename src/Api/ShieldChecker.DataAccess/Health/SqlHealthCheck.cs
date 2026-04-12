using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ShieldChecker.DataAccess.Health
{
    /// <summary>
    /// Verifies that the application can reach the SQL Server database by using
    /// EF Core's built-in <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnectAsync"/>.
    /// No third-party NuGet packages are required.
    /// </summary>
    public sealed class SqlHealthCheck : IHealthCheck
    {
        private readonly ShieldCheckerContext _context;

        public SqlHealthCheck(ShieldCheckerContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.Database.CanConnectAsync(cancellationToken);
                return HealthCheckResult.Healthy("SQL Server connection is healthy.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("SQL Server connection failed.", ex);
            }
        }
    }
}
