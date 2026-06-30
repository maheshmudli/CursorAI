using System.Text.RegularExpressions;

namespace ShiftPlatform.Services;

public static class SlugHelper
{
    public static string GenerateSlug(string companyName)
    {
        var slug = companyName.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            slug = "company";
        }

        return $"{slug}-{Guid.NewGuid():N}"[..Math.Min(60, slug.Length + 33)];
    }
}
