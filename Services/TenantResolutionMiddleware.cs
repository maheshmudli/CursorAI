using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;

namespace ShiftManagementPlatform.Services;

public sealed class TenantResolutionMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var slug = context.Request.RouteValues["tenantSlug"]?.ToString();
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();
            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.Slug == slug);

            if (tenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Tenant workspace was not found.");
                return;
            }

            context.RequestServices.GetRequiredService<ITenantProvider>()
                .SetTenant(tenant.Id, tenant.Slug);
        }

        await next(context);
    }
}
