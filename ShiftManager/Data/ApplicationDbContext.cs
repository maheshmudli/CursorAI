using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Models;
using ShiftManager.Services;

namespace ShiftManager.Data
{
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
        public DbSet<RegistrationAttempt> RegistrationAttempts => Set<RegistrationAttempt>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Global query filters for tenant isolation
            builder.Entity<Shift>().HasQueryFilter(s => s.TenantId == _tenantContext.CurrentTenantId || _tenantContext.CurrentTenantId == null);
            builder.Entity<ShiftAssignment>().HasQueryFilter(sa => sa.TenantId == _tenantContext.CurrentTenantId || _tenantContext.CurrentTenantId == null);
            builder.Entity<SwapRequest>().HasQueryFilter(sr => sr.TenantId == _tenantContext.CurrentTenantId || _tenantContext.CurrentTenantId == null);
            builder.Entity<PaymentRecord>().HasQueryFilter(pr => pr.TenantId == _tenantContext.CurrentTenantId || _tenantContext.CurrentTenantId == null);
            builder.Entity<SeatPurchase>().HasQueryFilter(sp => sp.TenantId == _tenantContext.CurrentTenantId || _tenantContext.CurrentTenantId == null);
            builder.Entity<AccessLog>().HasQueryFilter(al => al.TenantId == _tenantContext.CurrentTenantId || _tenantContext.CurrentTenantId == null);
            builder.Entity<ProvisioningRecord>().HasQueryFilter(pr => pr.TenantId == _tenantContext.CurrentTenantId || _tenantContext.CurrentTenantId == null);

            // Relationships
            builder.Entity<Shift>()
                .HasOne(s => s.Assignment)
                .WithOne(a => a.Shift)
                .HasForeignKey<ShiftAssignment>(a => a.ShiftId);

            builder.Entity<ShiftAssignment>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SwapRequest>()
                .HasOne(sr => sr.RequestingUser)
                .WithMany()
                .HasForeignKey(sr => sr.RequestingUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SwapRequest>()
                .HasOne(sr => sr.TargetUser)
                .WithMany()
                .HasForeignKey(sr => sr.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SwapRequest>()
                .HasOne(sr => sr.RequestingAssignment)
                .WithMany()
                .HasForeignKey(sr => sr.RequestingAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SwapRequest>()
                .HasOne(sr => sr.TargetAssignment)
                .WithMany()
                .HasForeignKey(sr => sr.TargetAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Tenant>()
                .HasOne(t => t.ProvisioningRecord)
                .WithOne(pr => pr.Tenant)
                .HasForeignKey<ProvisioningRecord>(pr => pr.TenantId);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AccessLog>()
                .HasOne(al => al.User)
                .WithMany()
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RegistrationAttempt>()
                .HasOne(r => r.Tenant)
                .WithMany()
                .HasForeignKey(r => r.TenantId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            builder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        }
    }
}
