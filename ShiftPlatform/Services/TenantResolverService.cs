using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;

namespace ShiftPlatform.Services;

public interface ITenantResolverService
{
    string? ExtractTenantSlugFromPath(string? path);
    Task<Tenant?> ResolveTenantBySlugAsync(string tenantSlug, CancellationToken cancellationToken = default);
}

public class TenantResolverService(ApplicationDbContext dbContext) : ITenantResolverService
{
    public string? ExtractTenantSlugFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !string.Equals(segments[0], "t", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return segments[1].Trim().ToLowerInvariant();
    }

    public Task<Tenant?> ResolveTenantBySlugAsync(string tenantSlug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        return dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, cancellationToken);
    }
}
