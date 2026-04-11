using Microsoft.EntityFrameworkCore;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.DataAccess
{
    public class ShieldCheckerContext : DbContext
    {
        public ShieldCheckerContext(DbContextOptions<ShieldCheckerContext> options)
            : base(options)
        {
        }

        public DbSet<SystemStatus> SystemStatus { get; set; }
        public DbSet<TestDefinition> UseCaseTests { get; set; }
        public DbSet<TestJob> TestJobs { get; set; }
        public DbSet<UserInfo> UserInfo { get; set; }
        public DbSet<Settings> Settings { get; set; }
        public DbSet<SchedulerMutex> SchedulerMutex { get; set; }
        public DbSet<AutoSchedule> AutoSchedule { get; set; }
        public DbSet<SharedTestDefinition> SharedTestLibrary { get; set; }
        public DbSet<SharedTestConsumption> SharedTestConsumptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemStatus>().ToTable("SystemStatus");

            modelBuilder.Entity<TestDefinition>()
                .ToTable("TestDefinition", t => t.IsTemporal())
                .Property(t => t.Enabled)
                .HasDefaultValueSql("1");
            modelBuilder.Entity<TestJob>().ToTable("TestJob");
            modelBuilder.Entity<SchedulerMutex>().ToTable("SchedulerMutex");
            modelBuilder.Entity<UserInfo>().ToTable("UserInfo", s => s.IsTemporal());
            modelBuilder.Entity<Settings>().ToTable("Settings", s => s.IsTemporal());
            modelBuilder.Entity<TestDefinition>()
                .HasOne(e => e.CreatedBy)
                .WithMany()
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TestDefinition>()
                .HasOne(e => e.ModifiedBy)
                .WithMany()
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AutoSchedule>().ToTable("AutoSchedule")
                .HasMany(e => e.TestDefinitions)
                .WithMany(e => e.AutoSchedules);

            modelBuilder.Entity<SharedTestDefinition>().ToTable("SharedTestDefinition")
                .HasOne(e => e.SubmittedBy)
                .WithMany()
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SharedTestDefinition>()
                .HasOne(e => e.ApprovedBy)
                .WithMany()
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SharedTestConsumption>().ToTable("SharedTestConsumption")
                .HasOne(e => e.ConsumedBy)
                .WithMany()
                .HasForeignKey(e => e.ConsumedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SharedTestConsumption>()
                .HasOne(e => e.SharedTestDefinition)
                .WithMany(e => e.Consumptions)
                .HasForeignKey(e => e.SharedTestDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SharedTestConsumption>()
                .HasIndex(e => new { e.SharedTestDefinitionId, e.ConsumedByUserId })
                .IsUnique();

            modelBuilder.Entity<TestDefinition>()
                .HasOne(e => e.SharedLibrarySource)
                .WithMany()
                .HasForeignKey(e => e.SharedLibrarySourceId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
