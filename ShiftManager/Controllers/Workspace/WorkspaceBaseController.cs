using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Workspace
{
    [Authorize]
    public abstract class WorkspaceBaseController : Controller
    {
        protected readonly ApplicationDbContext _db;
        protected readonly ITenantContext _tenantContext;

        protected WorkspaceBaseController(ApplicationDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!_tenantContext.CurrentTenantId.HasValue)
            {
                context.Result = NotFound("Tenant not found");
                return;
            }

            var tenant = await _db.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == _tenantContext.CurrentTenantId.Value);

            if (tenant == null || tenant.Status != TenantStatus.Active)
            {
                context.Result = NotFound("Tenant not found or inactive");
                return;
            }

            ViewBag.TenantSlug = tenant.Slug;
            ViewBag.TenantName = tenant.CompanyName;
            ViewBag.TenantId = tenant.Id;

            await next();
        }
    }
}
