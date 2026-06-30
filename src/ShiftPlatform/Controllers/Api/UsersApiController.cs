using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Services;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Controllers.Api;

[Route("t/{slug}/api/users")]
public class UsersApiController : TenantApiController
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersApiController(ITenantContext tenant, ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        : base(tenant)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> Get()
    {
        var users = await _db.Users.Where(u => u.TenantId == TenantId)
            .OrderBy(u => u.FullName).ToListAsync();

        var dtos = new List<UserDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            dtos.Add(new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? PlatformConstants.Roles.User
            });
        }
        return Ok(dtos);
    }

    /// <summary>
    /// Creates a member. The seat allowance is enforced here on the server: a
    /// company can never have more users than the seats it has paid for.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Create(UserCreateDto dto)
    {
        if (!IsAdmin) return Forbid();

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantId);
        if (tenant == null) return NotFound();

        var currentCount = await _db.Users.CountAsync(u => u.TenantId == TenantId);
        if (currentCount + 1 > tenant.SeatAllowance)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                error = $"You have used all {tenant.SeatAllowance} of your seats. Buy another seat for 5.00 AUD to add more users.",
                code = "seat_limit_reached",
                seatAllowance = tenant.SeatAllowance,
                userCount = currentCount
            });
        }

        if (await _userManager.FindByEmailAsync(dto.Email) != null)
            return BadRequest(new { error = $"An account already exists for {dto.Email}." });

        var tempPassword = GenerateTempPassword();
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            EmailConfirmed = true,
            FullName = dto.FullName,
            TenantId = TenantId
        };
        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });
        await _userManager.AddToRoleAsync(user, PlatformConstants.Roles.User);

        return Ok(new
        {
            user = new UserDto { Id = user.Id, FullName = user.FullName, Email = user.Email!, Role = PlatformConstants.Roles.User },
            tempPassword
        });
    }

    private static string GenerateTempPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        var rnd = Random.Shared;
        string pwd = $"{upper[rnd.Next(upper.Length)]}{lower[rnd.Next(lower.Length)]}{digits[rnd.Next(digits.Length)]}{symbols[rnd.Next(symbols.Length)]}";
        const string all = upper + lower + digits;
        for (int i = 0; i < 6; i++) pwd += all[rnd.Next(all.Length)];
        return pwd;
    }
}
