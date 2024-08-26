using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using EasyLotteryWasm;
using EasyLotteryDomain.Services;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;

var EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");


var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
  // 注入 YouTubeServiceHelper 服務
builder.Services.AddSingleton(sp => new YouTubeServiceHelper(sp.GetRequiredService<IConfiguration>(),"YoutubeDemo", "YouTube.Auth.Store"));

builder.Services
    .AddBlazorise( options =>
    {
        options.Immediate = true;
    } )
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

await builder.Build().RunAsync();