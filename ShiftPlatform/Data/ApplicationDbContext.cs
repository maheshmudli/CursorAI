using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Models;
using ShiftPlatform.Models.Interfaces;
using ShiftPlatform.Services;

namespace ShiftPlatform.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
    : IdentityDbContext<ApplicationUser>(options)
{
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

        builder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        builder.Entity<ApplicationUser>().HasIndex(u => new { u.TenantId, u.Email });
        builder.Entity<ApplicationUser>().Property(u => u.IsStaff).HasDefaultValue(true);
        builder.Entity<Tenant>().Property(t => t.FontFamily).HasDefaultValue("Aptos");

        builder.Entity<ShiftAssignment>()
            .HasOne(a => a.Shift)
            .WithMany(s => s.Assignments)
            .HasForeignKey(a => a.ShiftId);

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

        builder.Entity<Shift>()
            .HasQueryFilter(x => !tenantContext.CurrentTenantId.HasValue || x.TenantId == tenantContext.CurrentTenantId.Value);
        builder.Entity<ShiftAssignment>()
            .HasQueryFilter(x => !tenantContext.CurrentTenantId.HasValue || x.TenantId == tenantContext.CurrentTenantId.Value);
        builder.Entity<SwapRequest>()
            .HasQueryFilter(x => !tenantContext.CurrentTenantId.HasValue || x.TenantId == tenantContext.CurrentTenantId.Value);
        builder.Entity<AccessLog>()
            .HasQueryFilter(x => !tenantContext.CurrentTenantId.HasValue || x.TenantId == tenantContext.CurrentTenantId.Value);

        builder.Entity<PaymentRecord>()
            .HasIndex(p => p.StripePaymentIntentId)
            .IsUnique();
        builder.Entity<SeatPurchase>()
            .HasIndex(s => s.StripePaymentIntentId)
            .IsUnique();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantGuard();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyTenantGuard();
        return base.SaveChanges();
    }

    private void ApplyTenantGuard()
    {
        var tenantId = tenantContext.CurrentTenantId;
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State is EntityState.Added && tenantId.HasValue)
            {
                entry.Entity.TenantId = tenantId.Value;
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted &&
                tenantId.HasValue &&
                entry.Entity.TenantId != tenantId.Value)
            {
                throw new InvalidOperationException("Cross-tenant mutation blocked.");
            }
        }
    }
}
