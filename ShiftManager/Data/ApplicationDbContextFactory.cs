using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShiftManager.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            // Use SQLite for migrations at design time
            optionsBuilder.UseSqlite("Data Source=shiftmanager.db");

            return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
        }
    }
}
