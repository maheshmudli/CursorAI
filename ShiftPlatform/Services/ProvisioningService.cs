using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.Enums;

namespace ShiftPlatform.Services;

public interface IProvisioningService
{
    Task ProvisionWorkspaceAsync(Tenant tenant, CancellationToken cancellationToken = default);
}

public class ProvisioningService : IProvisioningService
{
    private readonly ApplicationDbContext _db;

    public ProvisioningService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task ProvisionWorkspaceAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var route = $"/{Constants.TenantRoutePrefix}/{tenant.Slug}";

        var record = new ProvisioningRecord
        {
            TenantId = tenant.Id,
            ProvisionedAt = DateTime.UtcNow,
            Route = route,
            Status = ProvisioningStatus.Completed
        };

        _db.ProvisioningRecords.Add(record);

        // Seed template starting data: default role labels as sample unassigned shifts for the next week
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var templateRoles = new[] { "Morning Shift", "Afternoon Shift", "Evening Shift" };

        for (var i = 1; i <= 3; i++)
        {
            _db.Shifts.Add(new Shift
            {
                TenantId = tenant.Id,
                Date = today.AddDays(i),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                RoleLabel = templateRoles[i - 1],
                Notes = "Template shift — assign to a team member",
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
