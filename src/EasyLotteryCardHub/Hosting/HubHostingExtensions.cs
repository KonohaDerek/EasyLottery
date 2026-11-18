using System.Text;
using System.Threading.RateLimiting;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Endpoints;
using EasyLotteryCardHub.Health;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EasyLotteryCardHub.Hosting;

public static class HubHostingExtensions
{
    public static void AddCardHubServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString,
        TimeProvider? timeProvider = null)
    {
        var tokenOptions = HubTokenSecurityOptions.Load(configuration);
        var passkeyOptions = StreamerPasskeyOptions.Load(configuration);
        services.AddSingleton(tokenOptions);
        services.AddSingleton(passkeyOptions);
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddSingleton<PasskeyCeremonyStore>();
        services.AddScoped<HubTokenService>();
        services.AddScoped<PlatformAdminService>();
        services.AddScoped<InviteRedemptionService>();
        services.AddScoped<SiteCredentialService>();
        services.AddScoped<StreamerPasskeyService>();
        services.AddScoped<SiteTenantStatusFilter>();
        services.AddScoped<StreamerTenantStatusFilter>();
        services.AddDbContext<HubDbContext>(options => options.UseNpgsql(connectionString));
        services.AddHealthChecks().AddCheck<PostgreSqlHealthCheck>("postgres", tags: ["ready"]);
        services.AddRouting();
        services.AddHubAuthentication(tokenOptions, configuration);

        var authenticationPermitLimit = Math.Max(5, configuration.GetValue<int?>("RateLimits:AuthenticationPerMinute") ?? 10);
        var adminPermitLimit = Math.Max(2, configuration.GetValue<int?>("RateLimits:AdminPerMinute") ?? 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = authenticationPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("streamer-sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("tenant_id")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("admin-sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = adminPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    public static void UseCardHubRequestPipeline(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("HubExceptionHandler");
            if (exception is HubServiceException serviceException)
            {
                context.Response.StatusCode = serviceException.StatusCode;
                await Results.Json(new HubApiError(serviceException.Code, serviceException.Message), statusCode: serviceException.StatusCode).ExecuteAsync(context);
                return;
            }

            if (exception is DbUpdateException { InnerException: PostgresException postgres } && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                var duplicate = postgres.ConstraintName switch
                {
                    "ux_streamers_email_normalized" => ("tenant_email_exists", "A streamer account already exists for this email."),
                    "ux_streamers_slug" => ("tenant_slug_exists", "The tenant slug is already in use."),
                    "ux_streamer_sites_base_url" => ("site_already_linked", "This site is already linked to a tenant."),
                    _ => ("conflict", "The request conflicts with existing data.")
                };
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await Results.Json(new HubApiError(duplicate.Item1, duplicate.Item2), statusCode: StatusCodes.Status409Conflict).ExecuteAsync(context);
                return;
            }

            logger.LogError(exception, "Unhandled Card Hub request error.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await Results.Json(new HubApiError("internal_error", "The request could not be completed."), statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
        }));
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
    }

    public static async Task InitializeCardHubDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        if (!app.Configuration.GetValue("Database:RunMigrationsOnStartup", true)) return;
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await SeedBootstrapAdminAsync(db, app.Configuration, scope.ServiceProvider.GetRequiredService<TimeProvider>(), cancellationToken);
    }

    public static async Task SeedBootstrapAdminAsync(
        HubDbContext db,
        IConfiguration configuration,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        var configuredEmail = configuration["PlatformAdmin:BootstrapEmail"];
        if (string.IsNullOrWhiteSpace(configuredEmail)) return;
        var normalizedEmail = HubValidation.NormalizeEmail(configuredEmail);
        if (await db.PlatformAdmins.AnyAsync(item => item.EmailNormalized == normalizedEmail, cancellationToken)) return;
        db.PlatformAdmins.Add(new PlatformAdmin
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            EmailNormalized = normalizedEmail,
            CreatedAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public static void ValidateProductionConfiguration(IConfiguration configuration)
    {
        var adminToken = configuration["PlatformAdmin:BootstrapToken"];
        var adminEmail = configuration["PlatformAdmin:BootstrapEmail"];
        if (string.IsNullOrWhiteSpace(adminToken) || Encoding.UTF8.GetByteCount(adminToken) < 32 || string.IsNullOrWhiteSpace(adminEmail))
            throw new InvalidOperationException("Production requires a 32-byte PlatformAdmin:BootstrapToken and PlatformAdmin:BootstrapEmail.");

        var issuerValue = configuration["Authentication:Issuer"];
        if (!Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer)
            || !string.Equals(issuer.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(issuer.UserInfo))
            throw new InvalidOperationException("Production requires Authentication:Issuer to be an absolute HTTPS URL.");

        var rpId = configuration["Passkey:RpId"]?.Trim();
        if (string.IsNullOrWhiteSpace(rpId) || string.Equals(rpId, "localhost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production requires Passkey:RpId to be the public relying-party domain.");

        var origins = configuration.GetSection("Passkey:Origins").GetChildren()
            .Select(item => item.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        if (origins.Length == 0 || origins.Any(origin => !IsPasskeyOrigin(origin, rpId)))
            throw new InvalidOperationException("Production requires HTTPS Passkey:Origins whose hosts match Passkey:RpId.");
    }

    private static bool IsPasskeyOrigin(string origin, string rpId) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && uri.AbsolutePath == "/"
        && (string.Equals(uri.Host, rpId, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + rpId, StringComparison.OrdinalIgnoreCase));
}
