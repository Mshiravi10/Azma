using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Azma.Identity.Api.Infrastructure;

public static class SlugGenerator
{
    private static readonly Regex InvalidChars = new("[^a-z0-9-]+", RegexOptions.Compiled);
    private static readonly Regex MultiDash = new("-{2,}", RegexOptions.Compiled);

    public static string From(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Guid.NewGuid().ToString("N")[..8];

        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch is '-' or '_') sb.Append('-');
        }

        var slug = InvalidChars.Replace(sb.ToString(), "-");
        slug = MultiDash.Replace(slug, "-").Trim('-');
        if (string.IsNullOrEmpty(slug)) slug = Guid.NewGuid().ToString("N")[..8];
        if (slug.Length > 48) slug = slug[..48].TrimEnd('-');
        return slug;
    }
}
