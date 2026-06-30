using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftManagementPlatform.Models;
using ShiftManagementPlatform.Services;

namespace ShiftManagementPlatform.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantProvider _tenantProvider;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RegistrationSubmission> RegistrationSubmissions => Set<RegistrationSubmission>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
    public DbSet<ProvisioningRecord> ProvisioningRecords => Set<ProvisioningRecord>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<SeatPurchase> SeatPurchases => Set<SeatPurchase>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ApplicationUser>()
            .HasQueryFilter(u => _tenantProvider.TenantId == null || u.TenantId == null || u.TenantId == _tenantProvider.TenantId);

        builder.Entity<Shift>()
            .HasQueryFilter(s => _tenantProvider.TenantId == null || s.TenantId == _tenantProvider.TenantId);
        builder.Entity<ShiftAssignment>()
            .HasQueryFilter(a => _tenantProvider.TenantId == null || a.TenantId == _tenantProvider.TenantId);
        builder.Entity<SwapRequest>()
            .HasQueryFilter(s => _tenantProvider.TenantId == null || s.TenantId == _tenantProvider.TenantId);
        builder.Entity<AccessLog>()
            .HasQueryFilter(l => _tenantProvider.TenantId == null || l.TenantId == _tenantProvider.TenantId);

        builder.Entity<ShiftAssignment>()
            .HasIndex(a => new { a.TenantId, a.ShiftId })
            .IsUnique();

        builder.Entity<ShiftAssignment>()
            .HasOne(a => a.Shift)
            .WithMany(s => s.Assignments)
            .HasForeignKey(a => a.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ShiftAssignment>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SwapRequest>()
            .HasOne(s => s.RequestingAssignment)
            .WithMany()
            .HasForeignKey(s => s.RequestingAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SwapRequest>()
            .HasOne(s => s.TargetAssignment)
            .WithMany()
            .HasForeignKey(s => s.TargetAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
