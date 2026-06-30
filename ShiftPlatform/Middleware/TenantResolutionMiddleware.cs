using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using System.Security.Claims;

namespace ShiftPlatform.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ApplicationDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 2 && segments[0].Equals(Constants.TenantRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var slug = segments[1];
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug);
            if (tenant != null)
            {
                tenantContext.SetTenant(tenant.Id, tenant.Slug);
                context.Items["TenantSlug"] = tenant.Slug;
            }
        }
        else if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst("TenantId")?.Value;
            if (int.TryParse(tenantIdClaim, out var tenantId))
            {
                var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant != null)
                {
                    tenantContext.SetTenant(tenant.Id, tenant.Slug);
                    context.Items["TenantSlug"] = tenant.Slug;
                }
            }
        }

        await _next(context);
    }
}

public class TenantClaimsMiddleware
{
    private readonly RequestDelegate _next;

    public TenantClaimsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirst("TenantId") == null &&
            !string.IsNullOrEmpty(context.User.Identity.Name))
        {
            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == context.User.Identity.Name);
            if (user?.TenantId != null)
            {
                var identity = (ClaimsIdentity)context.User.Identity;
                identity.AddClaim(new Claim("TenantId", user.TenantId.Value.ToString()));
            }
        }

        await _next(context);
    }
}
