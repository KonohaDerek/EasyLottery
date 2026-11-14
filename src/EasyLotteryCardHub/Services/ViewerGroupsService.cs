using System.Security.Claims;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryCardHub.Services;

public sealed class ViewerGroupsService(HubDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ViewerGroupResponse>> ListAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        var groups = await db.ViewerGroups.AsNoTracking()
            .Where(item => item.ViewerId == viewerId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var ids = groups.Select(item => item.Id).ToArray();
        var items = await db.ViewerGroupItems.AsNoTracking()
            .Where(item => ids.Contains(item.GroupId))
            .ToListAsync(cancellationToken);
        return groups.Select(group => ToResponse(group, items.Where(item => item.GroupId == group.Id))).ToArray();
    }

    public async Task<ViewerGroupResponse> CreateAsync(Guid viewerId, string? name, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        var now = timeProvider.GetUtcNow();
        var group = new ViewerGroup
        {
            Id = Guid.NewGuid(),
            ViewerId = viewerId,
            Name = normalized.Display,
            NameNormalized = normalized.Key,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ViewerGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(group, []);
    }

    public async Task<ViewerGroupResponse?> UpdateAsync(Guid viewerId, Guid groupId, string? name, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        var group = await db.ViewerGroups.SingleOrDefaultAsync(item => item.Id == groupId && item.ViewerId == viewerId, cancellationToken);
        if (group is null) return null;
        group.Name = normalized.Display;
        group.NameNormalized = normalized.Key;
        group.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        var items = await db.ViewerGroupItems.AsNoTracking().Where(item => item.GroupId == groupId).ToListAsync(cancellationToken);
        return ToResponse(group, items);
    }

    public async Task<bool> DeleteAsync(Guid viewerId, Guid groupId, CancellationToken cancellationToken)
    {
        var group = await db.ViewerGroups.SingleOrDefaultAsync(item => item.Id == groupId && item.ViewerId == viewerId, cancellationToken);
        if (group is null) return false;
        db.ViewerGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ViewerGroupResponse ToResponse(ViewerGroup group, IEnumerable<ViewerGroupItem> items) =>
        new(group.Id, group.Name, group.CreatedAt, group.UpdatedAt, items.Select(item => item.CardInstanceId).ToArray());

    private static (string Display, string Key) NormalizeName(string? name)
    {
        var display = name?.Trim() ?? string.Empty;
        if (display.Length is 0 or > 80 || display.Any(char.IsControl))
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_group_name_invalid", "viewer.group.name.invalid");
        return (display, display.ToUpperInvariant());
    }
}

public sealed record ViewerGroupResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Guid> CardInstanceIds);

public sealed class ViewerAccountFilter(HubDbContext db) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var principal = context.HttpContext.User;
        var viewerId = principal.FindFirstValue("viewer_id") ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(viewerId, out var id))
            return HubErrorResults.Unauthorized("invalid_viewer_token", "A valid viewer account is required.");

        if (!await db.Viewers.AnyAsync(item => item.Id == id, context.HttpContext.RequestAborted))
            return HubErrorResults.Unauthorized("viewer_account_unavailable", "The viewer account is no longer available.");

        context.HttpContext.Items[ViewerEndpointIdentity.ViewerIdItemKey] = id;
        return await next(context);
    }
}

public static class ViewerEndpointIdentity
{
    public const string ViewerIdItemKey = "card-hub-viewer-id";

    public static Guid GetViewerId(HttpContext context) => context.Items[ViewerIdItemKey] is Guid id
        ? id
        : throw new InvalidOperationException("The viewer endpoint filter did not set a viewer identity.");
}
