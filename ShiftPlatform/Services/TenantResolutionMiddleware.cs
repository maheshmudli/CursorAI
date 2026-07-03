namespace ShiftPlatform.Services;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantContext tenantContext,
        ITenantResolverService tenantResolver)
    {
        tenantContext.Clear();

        var slug = tenantResolver.ExtractTenantSlugFromPath(httpContext.Request.Path.Value);
        if (slug is not null)
        {
            var tenant = await tenantResolver.ResolveTenantBySlugAsync(slug);
            if (tenant is not null)
            {
                tenantContext.SetTenant(tenant.Id, tenant.Slug);
            }
        }

        await next(httpContext);
    }
}
