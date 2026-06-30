using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;

namespace ShiftPlatform.Services;

public static class SlugGenerator
{
    public static string Slugify(string input)
    {
        var lower = input.Trim().ToLowerInvariant();
        var slug = Regex.Replace(lower, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "company" : slug;
    }

    /// <summary>Returns a slug that is unique across tenants and pending registrations.</summary>
    public static async Task<string> GenerateUniqueAsync(ApplicationDbContext db, string companyName)
    {
        var baseSlug = Slugify(companyName);
        var slug = baseSlug;
        var suffix = 1;
        while (await db.Tenants.AnyAsync(t => t.Slug == slug)
               || await db.PendingRegistrations.AnyAsync(p => p.Slug == slug && p.Status != "Provisioned" && p.Status != "Failed"))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }
        return slug;
    }
}
