using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace ShieldChecker.DataAccess
{
    public static class DbInitializer
    {
        public static void Initialize(ShieldCheckerContext context, IHostEnvironment hostEnvironment)
        {
            if (hostEnvironment.IsDevelopment())
            {
                string sql = System.IO.File.ReadAllText("Models\\Db\\sql-initialization.sql");
                sql = sql.Replace("_DomainFQDN_", "shieldchecker.local");
                context.Database.ExecuteSqlRaw(sql);
                var status = context.SystemStatus.Where(s => s.ID == 1).FirstOrDefault();
                if (status != null)
                {
                    status.IsFirstRunCompleted = true;
                    context.SystemStatus.Update(status);
                    var t = context.SaveChangesAsync();
                    t.Wait();
                }
            }

            if (context.SystemStatus.Count() != 1)
            {
                throw new Exception("DBNotInitialized: SystemStatus not found");
            }
            if (context.Settings.Count() != 1)
            {
                throw new Exception("DBNotInitialized: Settings not found");
            }
            if (context.UserInfo.Where(u => u.Id == new Guid("00000000-0000-0000-0000-000000000000")).Count() == 0)
            {
                throw new Exception("DBNotInitialized: System UserInfo not found");
            }
        }
    }
}
