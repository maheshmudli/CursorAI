using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftManager.Controllers.Api
{
    [ApiController]
    [Authorize]
    [Route("t/{slug}/api/users")]
    public class UsersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersApiController(ApplicationDbContext db, ITenantContext tenantContext, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _tenantContext = tenantContext;
            _userManager = userManager;
        }

        private async Task<bool> EnsureTenantAsync(string slug)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slug);
            if (tenant == null) return false;
            _tenantContext.CurrentTenantId = tenant.Id;
            return true;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers(string slug)
        {
            if (!await EnsureTenantAsync(slug)) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.TenantId != _tenantContext.CurrentTenantId)
                return Forbid();

            var users = await _db.Users.IgnoreQueryFilters()
                .Where(u => u.TenantId == _tenantContext.CurrentTenantId && u.IsActive)
                .ToListAsync();

            var result = new System.Collections.Generic.List<object>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                result.Add(new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    Role = roles.FirstOrDefault() ?? "User"
                });
            }
            return Ok(result);
        }
    }
}
