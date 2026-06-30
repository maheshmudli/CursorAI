using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShiftManager.Services
{
    public interface ITenantProvisioningService
    {
        Task<(Tenant tenant, string adminTempPassword, List<(ApplicationUser user, string tempPassword)> users)> ProvisionTenantAsync(
            string companyName,
            string adminFullName,
            string adminEmail,
            string adminPassword,
            List<(string fullName, string email)> additionalUsers,
            string stripePaymentIntentId);
    }

    public class TenantProvisioningService : ITenantProvisioningService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantContext _tenantContext;

        public TenantProvisioningService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ITenantContext tenantContext)
        {
            _db = db;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        public async Task<(Tenant tenant, string adminTempPassword, List<(ApplicationUser user, string tempPassword)> users)> ProvisionTenantAsync(
            string companyName,
            string adminFullName,
            string adminEmail,
            string adminPassword,
            List<(string fullName, string email)> additionalUsers,
            string stripePaymentIntentId)
        {
            // Generate unique slug
            var slug = GenerateSlug(companyName);
            slug = await EnsureUniqueSlugAsync(slug);

            // Create tenant (bypass filter for creation)
            var savedTenantId = _tenantContext.CurrentTenantId;
            _tenantContext.CurrentTenantId = null;

            var tenant = new Tenant
            {
                CompanyName = companyName,
                Slug = slug,
                CreatedAt = DateTime.UtcNow,
                Status = TenantStatus.Active,
                Tier = TenantTier.Base,
                SeatAllowance = 5
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();

            // Set tenant context so new records get the right TenantId
            _tenantContext.CurrentTenantId = tenant.Id;

            // Create admin user
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = adminFullName,
                TenantId = tenant.Id,
                EmailConfirmed = true,
                IsActive = true
            };
            var adminResult = await _userManager.CreateAsync(adminUser, adminPassword);
            if (!adminResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create admin: {string.Join(", ", adminResult.Errors.Select(e => e.Description))}");
            }
            await _userManager.AddToRoleAsync(adminUser, "CompanyAdmin");

            // Create additional users
            var createdUsers = new List<(ApplicationUser user, string tempPassword)>();
            foreach (var (fullName, email) in additionalUsers)
            {
                var tempPassword = GenerateTemporaryPassword();
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    TenantId = tenant.Id,
                    EmailConfirmed = true,
                    IsActive = true,
                    TemporaryPassword = tempPassword,
                    MustChangePassword = true
                };
                var result = await _userManager.CreateAsync(user, tempPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
                await _userManager.AddToRoleAsync(user, "User");
                createdUsers.Add((user, tempPassword));
            }

            // Create provisioning record
            var provisioningRecord = new ProvisioningRecord
            {
                TenantId = tenant.Id,
                ProvisionedAt = DateTime.UtcNow,
                Route = $"/t/{slug}",
                Status = ProvisioningStatus.Completed
            };
            _db.ProvisioningRecords.Add(provisioningRecord);

            // Seed template shifts for the tenant
            await SeedTemplateDataAsync(tenant.Id, adminUser.Id);

            await _db.SaveChangesAsync();

            _tenantContext.CurrentTenantId = savedTenantId;

            return (tenant, adminPassword, createdUsers);
        }

        private Task SeedTemplateDataAsync(int tenantId, string adminUserId)
        {
            var today = DateTime.UtcNow.Date;
            var shifts = new[]
            {
                new Shift { TenantId = tenantId, Date = today.AddDays(1), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), RoleLabel = "Morning Shift", Notes = "Welcome shift", CreatedBy = adminUserId, CreatedAt = DateTime.UtcNow },
                new Shift { TenantId = tenantId, Date = today.AddDays(2), StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(21, 0, 0), RoleLabel = "Afternoon Shift", Notes = "Afternoon coverage", CreatedBy = adminUserId, CreatedAt = DateTime.UtcNow },
                new Shift { TenantId = tenantId, Date = today.AddDays(3), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0), RoleLabel = "Morning Shift", Notes = null, CreatedBy = adminUserId, CreatedAt = DateTime.UtcNow }
            };
            _db.Shifts.AddRange(shifts);
            return Task.CompletedTask;
        }

        private static string GenerateSlug(string companyName)
        {
            var slug = companyName.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = slug.Trim('-');
            if (slug.Length > 50) slug = slug[..50];
            return string.IsNullOrEmpty(slug) ? "company" : slug;
        }

        private async Task<string> EnsureUniqueSlugAsync(string baseSlug)
        {
            var slug = baseSlug;
            var counter = 1;
            while (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug))
            {
                slug = $"{baseSlug}-{counter++}";
            }
            return slug;
        }

        private static string GenerateTemporaryPassword()
        {
            var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$";
            var rng = new Random();
            var password = new char[12];
            password[0] = "ABCDEFGHJKLMNPQRSTUVWXYZ"[rng.Next(24)];
            password[1] = "abcdefghijkmnopqrstuvwxyz"[rng.Next(25)];
            password[2] = "23456789"[rng.Next(8)];
            password[3] = "!@#$"[rng.Next(4)];
            for (int i = 4; i < 12; i++)
                password[i] = chars[rng.Next(chars.Length)];
            return new string(password.OrderBy(_ => rng.Next()).ToArray());
        }
    }
}
