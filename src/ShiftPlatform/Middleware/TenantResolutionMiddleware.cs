using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Services;

namespace ShiftPlatform.Middleware;

/// <summary>
/// Resolves the current tenant from the route (path routing: /t/{slug}/...),
/// populates the scoped <see cref="ITenantContext"/> that drives the EF global
/// query filter, and blocks any authenticated user from reaching a tenant that
/// is not their own.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, ITenantContext tenantContext)
    {
        var slug = context.Request.RouteValues.TryGetValue("slug", out var raw) ? raw as string : null;

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug);
            if (tenant == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync($"Unknown workspace '{slug}'.");
                return;
            }

            tenantContext.SetTenant(tenant.Id, tenant.Slug);

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var isPlatformOwner = context.User.IsInRole(PlatformConstants.Roles.PlatformOwner);
                var tenantClaim = context.User.FindFirst(AdditionalUserClaimsPrincipalFactory.TenantIdClaim)?.Value;

                // Platform Owners are not part of any tenant and are not granted
                // access to a tenant's workspace data here. Every other user must
                // belong to exactly the tenant they are trying to reach.
                if (isPlatformOwner || tenantClaim != tenant.Id.ToString())
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("You do not have access to this workspace.");
                    return;
                }
            }
        }

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
