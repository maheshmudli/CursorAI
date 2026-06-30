using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Models;
using ShiftPlatform.Services;

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
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();

    /// <summary>
    /// Current tenant id used by the global query filters. Reading the tenant
    /// context lazily here means EF re-evaluates it per query rather than baking
    /// a value into the cached model.
    /// </summary>
    private int CurrentTenantId => _tenantContext.TenantId ?? 0;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();

        // All tenant FKs use NO ACTION (Restrict) to avoid SQL Server's multiple
        // cascade-path error; tenants are not hard-deleted in normal operation.
        builder.Entity<ApplicationUser>().HasOne(u => u.Tenant).WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Shift>().HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ShiftAssignment>().HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SwapRequest>().HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProvisioningRecord>().HasOne(r => r.Tenant).WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PaymentRecord>().HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SeatPurchase>().HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AccessLog>().HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Shift>()
            .HasOne(s => s.Assignment)
            .WithOne(a => a.Shift!)
            .HasForeignKey<ShiftAssignment>(a => a.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        // Avoid multiple cascade paths on SQL Server for assignment relations.
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

        builder.Entity<SwapRequest>()
            .HasOne(s => s.RequestingUser)
            .WithMany()
            .HasForeignKey(s => s.RequestingUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SwapRequest>()
            .HasOne(s => s.TargetUser)
            .WithMany()
            .HasForeignKey(s => s.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PendingRegistration>().HasIndex(p => p.Token).IsUnique();
        builder.Entity<AccessLog>().HasIndex(a => a.UserId).IsUnique();

        // Global query filters: every tenant-owned entity is automatically
        // constrained to the current tenant. Platform-level queries that must
        // span tenants use IgnoreQueryFilters() explicitly.
        builder.Entity<Shift>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        builder.Entity<ShiftAssignment>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        builder.Entity<SwapRequest>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }
}
