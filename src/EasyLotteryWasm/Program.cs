using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using EasyLotteryDomain.Database;
using EasyLotteryDomain.Services;
using EasyLotteryWasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Serilog;

var EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var configuration = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                 .AddJsonFile($"appsettings.{EnvironmentName}.json", optional: true, reloadOnChange: true)
                 .Build();

var builder = WebAssemblyHostBuilder.CreateDefault(args);

Log.Logger = new LoggerConfiguration()
                    //.ReadFrom.Configuration(configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithThreadName()
                    .WriteTo.BrowserConsole()
                    .WriteTo.Console()
                    .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Configuration.AddConfiguration(configuration);

    
// 根據 appsettings 設定選擇資料庫提供者
// Web 環境使用 InMemory（WASM 無法載入原生 SQLite）
// Tauri 環境使用 Sqlite
var dbProvider = configuration["Database:Provider"] ?? "InMemory";
builder.Services.AddDbContextFactory<EasyLotteryContext>(opt =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = configuration["Database:SqliteConnectionString"] ?? "Data Source=easylottery.sqlite";
        opt.UseSqlite(connectionString);
    }
    else
    {
        opt.UseInMemoryDatabase("EasyLottery");
    }
});


builder.Services.AddDistributedMemoryCache();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
  // 注入 YouTubeServiceHelper 服務

// 添加服務
builder.Services.AddSingleton<YouTubeServiceHelper>();
builder.Services.AddScoped<PokeService>();
builder.Services.AddScoped<RouletteService>();
// builder.Services.AddSingleton(sp => new YouTubeServiceHelper(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<YouTubeServiceHelper>>()));

builder.Services
    .AddBlazorise( options =>
    {
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();



builder.Logging.AddSerilog(Log.Logger);

await builder.Build().RunAsync();