using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Middleware;
using ShiftPlatform.Models;
using ShiftPlatform.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<AdditionalUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Stripe: bind options and pick the real gateway when a secret key is present,
// otherwise fall back to the in-memory dev fake so the flow can be tested.
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
var stripeOptions = builder.Configuration.GetSection("Stripe").Get<StripeOptions>() ?? new StripeOptions();
if (stripeOptions.IsConfigured)
    builder.Services.AddScoped<IPaymentGateway, StripePaymentGateway>();
else
    builder.Services.AddScoped<IPaymentGateway, FakePaymentGateway>();

builder.Services.AddScoped<IProvisioningService, ProvisioningService>();
builder.Services.AddScoped<ISeatService, SeatService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// After auth + routing so it can read the {slug} route value and the user's claims.
app.UseTenantResolution();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(services);
}

app.Run();

public partial class Program { }
