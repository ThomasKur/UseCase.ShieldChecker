using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShieldChecker.DataAccess
{
    /// <summary>
    /// Design-time factory used by EF Core tools (dotnet ef migrations add).
    /// Provides a ShieldCheckerContext with an in-memory SQL connection string
    /// so migrations can be generated without a live database.
    /// </summary>
    public class ShieldCheckerContextFactory : IDesignTimeDbContextFactory<ShieldCheckerContext>
    {
        public ShieldCheckerContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ShieldCheckerContext>();
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=ShieldCheckerDesignTime;Trusted_Connection=True;");
            return new ShieldCheckerContext(optionsBuilder.Options);
        }
    }
}
