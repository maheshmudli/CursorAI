using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShiftManagementPlatform.Services;

namespace ShiftManagementPlatform.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ShiftManagementPlatform;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        return new ApplicationDbContext(options, new TenantProvider());
    }
}
