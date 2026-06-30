using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models.Entities;

namespace ShiftPlatform.Services;

public interface IAccessLogService
{
    Task RecordLoginAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}

public class AccessLogService : IAccessLogService
{
    private readonly ApplicationDbContext _db;

    public AccessLogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RecordLoginAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (!user.TenantId.HasValue) return;

        var existing = await _db.AccessLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.TenantId == user.TenantId && a.UserId == user.Id, cancellationToken);

        if (existing != null)
        {
            existing.LastLoginAt = DateTime.UtcNow;
        }
        else
        {
            _db.AccessLogs.Add(new AccessLog
            {
                TenantId = user.TenantId.Value,
                UserId = user.Id,
                LastLoginAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
