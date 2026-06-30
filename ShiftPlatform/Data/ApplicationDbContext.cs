using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;

namespace ShiftPlatform.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
    public DbSet<ProvisioningRecord> ProvisioningRecords => Set<ProvisioningRecord>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<SeatPurchase> SeatPurchases => Set<SeatPurchase>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();
    public DbSet<RegistrationSubmission> RegistrationSubmissions => Set<RegistrationSubmission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.CompanyName).HasMaxLength(200);
            e.Property(t => t.Slug).HasMaxLength(100);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(200);
            e.HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Shift>(e =>
        {
            e.Property(s => s.RoleLabel).HasMaxLength(100);
            e.Property(s => s.Notes).HasMaxLength(1000);
            e.HasOne(s => s.Tenant).WithMany(t => t.Shifts).HasForeignKey(s => s.TenantId);
            e.HasQueryFilter(s => !_tenantContext.HasTenant || s.TenantId == _tenantContext.TenantId);
        });

        builder.Entity<ShiftAssignment>(e =>
        {
            e.HasOne(a => a.Shift).WithOne(s => s.Assignment).HasForeignKey<ShiftAssignment>(a => a.ShiftId);
            e.HasOne(a => a.User).WithMany(u => u.ShiftAssignments).HasForeignKey(a => a.UserId);
            e.HasQueryFilter(a => !_tenantContext.HasTenant || a.TenantId == _tenantContext.TenantId);
        });

        builder.Entity<SwapRequest>(e =>
        {
            e.HasOne(s => s.RequestingAssignment).WithMany().HasForeignKey(s => s.RequestingAssignmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.TargetAssignment).WithMany().HasForeignKey(s => s.TargetAssignmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(s => !_tenantContext.HasTenant || s.TenantId == _tenantContext.TenantId);
        });

        builder.Entity<AccessLog>(e =>
        {
            e.HasIndex(a => new { a.TenantId, a.UserId }).IsUnique();
            e.HasQueryFilter(a => !_tenantContext.HasTenant || a.TenantId == _tenantContext.TenantId);
        });

        builder.Entity<RegistrationSubmission>(e =>
        {
            e.Property(r => r.CompanyName).HasMaxLength(200);
            e.Property(r => r.AdminEmail).HasMaxLength(256);
            e.Property(r => r.AdminFullName).HasMaxLength(200);
        });
    }
}
