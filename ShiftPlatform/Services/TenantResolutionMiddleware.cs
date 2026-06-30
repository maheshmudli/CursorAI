using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;

namespace ShiftPlatform.Services;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, ITenantContext tenantContext, IServiceProvider serviceProvider)
    {
        tenantContext.Clear();

        var path = httpContext.Request.Path.Value ?? string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && string.Equals(segments[0], "t", StringComparison.OrdinalIgnoreCase))
        {
            var slug = segments[1].Trim().ToLowerInvariant();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug);
            if (tenant is not null)
            {
                tenantContext.SetTenant(tenant.Id, tenant.Slug);
            }
        }

        await next(httpContext);
    }
}
