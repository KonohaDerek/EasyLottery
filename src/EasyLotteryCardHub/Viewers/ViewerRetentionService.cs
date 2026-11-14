using EasyLotteryCardHub.Data;
using Npgsql;

namespace EasyLotteryCardHub.Viewers;

public sealed class ViewerRetentionService(HubDatabase database, TimeProvider timeProvider, ILogger<ViewerRetentionService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan ExpiredCodeGrace = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredAsync(stoppingToken);
                await Task.Delay(Interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Viewer authentication data retention cleanup failed.");
                await Task.Delay(Interval, timeProvider, stoppingToken);
            }
        }
    }

    internal async Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        if (database.DataSource is null) return;
        var now = timeProvider.GetUtcNow();
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var cancel = new NpgsqlCommand(
            "UPDATE viewer_email_outbox o SET cancelled_at_utc = @now FROM viewer_login_codes c WHERE o.login_code_id = c.id AND o.delivered_at_utc IS NULL AND o.cancelled_at_utc IS NULL AND c.expires_at_utc <= @now",
            connection, transaction))
        {
            cancel.Parameters.AddWithValue("now", now);
            await cancel.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var delete = new NpgsqlCommand(
            "DELETE FROM viewer_login_codes WHERE expires_at_utc <= @delete_before",
            connection, transaction))
        {
            delete.Parameters.AddWithValue("delete_before", now.Subtract(ExpiredCodeGrace));
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
