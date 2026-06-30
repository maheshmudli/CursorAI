using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Services;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Middleware
{
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ApplicationDbContext db)
        {
            // Path routing: /t/{slug}/...
            var path = context.Request.Path.Value ?? string.Empty;
            var segments = path.TrimStart('/').Split('/');

            if (segments.Length >= 2 && segments[0] == "t")
            {
                var slug = segments[1].ToLowerInvariant();
                // Bypass filter for tenant lookup
                var tenant = await db.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Slug == slug);

                if (tenant != null)
                {
                    tenantContext.CurrentTenantId = tenant.Id;
                    tenantContext.CurrentTenantSlug = tenant.Slug;
                }
            }

            await _next(context);
        }
    }
}
