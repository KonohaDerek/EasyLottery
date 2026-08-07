using System.Text.Json;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.DonateActivities;

public sealed class SqliteDonateActivityRepository(IOptions<StorageProviderOptions> options) : IDonateLotteryActivityRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DonateLotteryActivity>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM donate_activities ORDER BY id";
        var result = new List<DonateLotteryActivity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var activity = JsonSerializer.Deserialize<DonateLotteryActivity>(reader.GetString(0), JsonOptions);
            if (activity is not null) result.Add(activity);
        }
        return result;
    }

    public async Task<DonateLotteryActivity?> GetByIdAsync(int activityId, CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken)).FirstOrDefault(activity => activity.Id == activityId);

    public async Task SaveSnapshotAsync(IReadOnlyList<DonateLotteryActivity> activities, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = (SqliteTransaction)transaction;
            clear.CommandText = "DELETE FROM donate_activities";
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var activity in activities)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = "INSERT INTO donate_activities (id, public_id, name, type, minimum_donation_amount, starts_at_utc, ends_at_utc, animation, is_enabled, payload_json) VALUES ($id, $publicId, $name, $type, $minimum, $starts, $ends, $animation, $enabled, $payload)";
            insert.Parameters.AddWithValue("$id", activity.Id);
            insert.Parameters.AddWithValue("$publicId", activity.PublicId.ToString("D"));
            insert.Parameters.AddWithValue("$name", activity.Name);
            insert.Parameters.AddWithValue("$type", (int)activity.Type);
            insert.Parameters.AddWithValue("$minimum", activity.MinimumDonationAmount);
            insert.Parameters.AddWithValue("$starts", activity.StartsAtUtc.ToString("O"));
            insert.Parameters.AddWithValue("$ends", activity.EndsAtUtc.ToString("O"));
            insert.Parameters.AddWithValue("$animation", (int)activity.Animation);
            insert.Parameters.AddWithValue("$enabled", activity.IsEnabled ? 1 : 0);
            insert.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(activity, JsonOptions));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
