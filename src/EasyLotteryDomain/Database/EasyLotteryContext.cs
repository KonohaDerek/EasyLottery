using EasyLotteryDomain.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryDomain.Database
{
    public enum DBProvider
    {
        SqlServer,
        Sqlite,
        Postgres,
        InMemory
    }

    public class EasyLotteryContext : DbContext
    {
         private static readonly bool[] s_migrated = { false };

        public EasyLotteryContext(DbContextOptions<EasyLotteryContext> options)
          : base(options)
        {
            this.Database.EnsureCreated();
            if (!s_migrated[0])
            {
                lock (s_migrated)
                {
                    if (!s_migrated[0])
                    {

                        this.Database.Migrate();
                        s_migrated[0] = true;
                    }
                }
            }
        }

        protected override void OnConfiguring(
           DbContextOptionsBuilder optionsBuilder)
        {

        }


        public DbSet<SystemSetting> SystemSettings { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
            new SystemSettingEntityTypeConfiguration().Configure(modelBuilder.Entity<SystemSetting>());
        }
    }
}