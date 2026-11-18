using EasyLotteryCardHub.Hosting;
using EasyLotteryCardHub.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("CardHub");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:CardHub is required.");

builder.Services.AddCardHubServices(builder.Configuration, connectionString);
if (builder.Environment.IsProduction()) HubHostingExtensions.ValidateProductionConfiguration(builder.Configuration);

var app = builder.Build();
app.UseCardHubRequestPipeline();
app.MapHubEndpoints();
await app.InitializeCardHubDatabaseAsync();
await app.RunAsync();

public partial class Program { }
