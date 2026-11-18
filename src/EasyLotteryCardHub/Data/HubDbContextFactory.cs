using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EasyLotteryCardHub.Data;

public sealed class HubDbContextFactory : IDesignTimeDbContextFactory<HubDbContext>
{
    public HubDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CardHub")
            ?? Environment.GetEnvironmentVariable("CARDHUB_DESIGNTIME_CONNECTION")
            ?? "Host=localhost;Database=easy_lottery_card_hub;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<HubDbContext>().UseNpgsql(connectionString).Options;
        return new HubDbContext(options);
    }
}
