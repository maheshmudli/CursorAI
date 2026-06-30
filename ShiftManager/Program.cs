using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShiftManager.Data;
using ShiftManager.Middleware;
using ShiftManager.Models;
using ShiftManager.Services;

var builder = WebApplication.CreateBuilder(args);

// Database - use SQLite for development if USE_SQLITE env var is set or no SQL Server connection
var useSqlite = Environment.GetEnvironmentVariable("USE_SQLITE") == "true" ||
                builder.Configuration.GetConnectionString("DefaultConnection")?.Contains("localhost") == true &&
                !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

if (useSqlite)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=shiftmanager.db"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Tenant context (scoped - per request)
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = System.TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Authentication / Authorization
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = System.TimeSpan.FromDays(1);
    // Return 401/403 JSON for API requests instead of redirecting
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api") ||
            ctx.Request.Path.Value?.Contains("/api/") == true)
        {
            ctx.Response.StatusCode = 401;
            return System.Threading.Tasks.Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return System.Threading.Tasks.Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api") ||
            ctx.Request.Path.Value?.Contains("/api/") == true)
        {
            ctx.Response.StatusCode = 403;
            return System.Threading.Tasks.Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return System.Threading.Tasks.Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

// MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Tenant resolution must happen before auth
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Tenant workspace routes
app.MapControllerRoute(
    name: "tenant-route",
    pattern: "t/{slug}/{controller=WorkspaceHome}/{action=Index}/{id?}");

// Platform routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed data
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

app.Run();
