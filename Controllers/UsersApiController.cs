using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Data;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.Services;

namespace ShiftManagementPlatform.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.CompanyAdmin},{Roles.User}")]
[Route("t/{tenantSlug}/api/users")]
public sealed class UsersApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public UsersApiController(ApplicationDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!TenantAllowed())
        {
            return Forbid();
        }

        var users = await _db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToListAsync();
        return Ok(users);
    }

    private bool TenantAllowed() =>
        _tenantProvider.TenantId is not null &&
        User.FindFirst("tenant_id")?.Value == _tenantProvider.TenantId.Value.ToString();
}
