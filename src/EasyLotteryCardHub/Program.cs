using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Endpoints;
using EasyLotteryCardHub.Health;
using EasyLotteryCardHub.Invites;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Email;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SiteAccessTokenIssuer>();
builder.Services.AddSingleton<IdentityHasher>();
builder.Services.AddSingleton<PostgresCardHubStore>();
builder.Services.AddHealthChecks().AddCheck<CardHubDatabaseHealthCheck>("postgres", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("site-token", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("hub-admin", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("invite-registration", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("streamer-management", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var connectionString = builder.Configuration.GetConnectionString("CardHub");
var dataSource = string.IsNullOrWhiteSpace(connectionString) ? null : NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(new HubDatabase(dataSource));
builder.Services.AddHostedService<HubDatabaseMigrator>();
builder.Services.AddSingleton<InviteEmailCipher>();
builder.Services.AddSingleton<PostgresInviteEmailOutbox>();
builder.Services.AddSingleton<CardHubInviteService>();
builder.Services.AddSingleton<StreamerPasskeyLoginService>();

if (builder.Environment.IsProduction())
{
    PasskeyConfiguration.ValidateProduction(builder.Configuration);
    IdentityHasher.ValidateConfiguration(builder.Configuration);
}

var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.UseRateLimiter();
app.MapAdminEndpoints();
app.MapInviteEndpoints();
app.MapStreamerPasskeyLoginEndpoints();
app.MapSiteOAuthEndpoints();
app.MapSiteRegistrationEndpoints();
app.Run();

public partial class Program;
