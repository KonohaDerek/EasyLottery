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
            // InMemory provider 不支援 Migrate()，僅在關聯式資料庫時執行
            var isInMemoryProvider = string.Equals(
                this.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal);
            if (!s_migrated[0] && !isInMemoryProvider)
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

        protected EasyLotteryContext(DbContextOptions<EasyLotteryContext> options, bool skipMigration)
          : base(options)
        {
            this.Database.EnsureCreated();
        }

        protected override void OnConfiguring(
           DbContextOptionsBuilder optionsBuilder)
        {

        }


        public DbSet<SystemSetting> SystemSettings { get; set; }

        public DbSet<PokeTemplate> PokeTemplates { get; set; }

        public DbSet<PokeCell> PokeCells { get; set; }

        public DbSet<RouletteTemplate> RouletteTemplates { get; set; }

        public DbSet<RouletteSegment> RouletteSegments { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
            new SystemSettingEntityTypeConfiguration().Configure(modelBuilder.Entity<SystemSetting>());
            new PokeTemplateEntityTypeConfiguration().Configure(modelBuilder.Entity<PokeTemplate>());
            new PokeCellEntityTypeConfiguration().Configure(modelBuilder.Entity<PokeCell>());
            new RouletteTemplateEntityTypeConfiguration().Configure(modelBuilder.Entity<RouletteTemplate>());
            new RouletteSegmentEntityTypeConfiguration().Configure(modelBuilder.Entity<RouletteSegment>());
        }
    }
}