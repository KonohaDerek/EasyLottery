using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using EasyLotteryDomain.Services;
using EasyLotteryWasm;
using EasyLotteryWasm.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Serilog;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.HostEnvironment.Environment}.json", optional: true, reloadOnChange: true);

Log.Logger = new LoggerConfiguration()
                    //.ReadFrom.Configuration(builder.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithThreadName()
                    .WriteTo.BrowserConsole()
                    .WriteTo.Console()
                    .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IEasyLotteryConfigStore, YamlEasyLotteryConfigStore>();
builder.Services.AddScoped<IOvertimeFeedStore, BrowserOvertimeFeedStore>();
builder.Services.AddScoped<SystemSettingsService>();
builder.Services.AddScoped<ObsLayoutService>();
builder.Services.AddScoped<SoundCueService>();
builder.Services.AddScoped<ActivityResultApiClient>();
builder.Services.AddScoped<DonateLotteryActivityApiClient>();
builder.Services.AddScoped<ObsSessionService>();
builder.Services.AddScoped<SettingsResourceApiClient>();
builder.Services.AddScoped<AiCongratulationClient>();
builder.Services.AddScoped<ResultNotificationService>();
builder.Services.AddScoped<VisualStyleService>();
builder.Services.AddScoped<OvertimeFeedClient>();
builder.Services.AddScoped<OvertimeRealtimeClient>();
builder.Services.AddTransient<LiveDrawSessionClient>();
builder.Services.AddScoped<TunnelRuntimeClient>();
builder.Services.AddScoped<EasyLotteryAuditService>();

// 添加服務
builder.Services.AddScoped<YouTubeServiceHelper>();
builder.Services.AddScoped<PokeTemplateApiClient>();
builder.Services.AddScoped<RouletteTemplateApiClient>();
builder.Services.AddScoped<ObsAssetApiClient>();

builder.Services
    .AddBlazorise(options =>
    {
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

builder.Logging.AddSerilog(Log.Logger);

await builder.Build().RunAsync();
