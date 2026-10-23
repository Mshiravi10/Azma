using System.Text.RegularExpressions;

namespace Azma.Automation;

public static partial class ClickTargetSelectors
{
    public static IReadOnlyList<string> Expand(string? selector, IReadOnlyList<string>? alternates)
    {
        var recorded = new List<string>();
        Add(recorded, selector);
        if (alternates is not null)
        {
            foreach (var alternate in alternates)
                Add(recorded, alternate);
        }

        var expanded = new List<string>();
        foreach (var candidate in recorded)
        {
            Add(expanded, PreferClickableAncestor(candidate));
            Add(expanded, candidate);
        }

        return expanded;
    }

    public static string? PreferClickableAncestor(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector) || !selector.Contains(" > ", StringComparison.Ordinal))
            return null;

        var parts = selector.Split(" > ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var end = parts.Length;
        while (end > 0 && GraphicPart().IsMatch(parts[end - 1]))
            end--;

        if (end == 0 || end == parts.Length)
            return null;

        var controlEnd = end;
        while (controlEnd > 1 && InlineWrapperPart().IsMatch(parts[controlEnd - 1]))
            controlEnd--;

        if (controlEnd < end && ControlPart().IsMatch(parts[controlEnd - 1]))
            end = controlEnd;

        var healed = string.Join(" > ", parts.Take(end));
        return string.Equals(healed, selector, StringComparison.Ordinal) ? null : healed;
    }

    private static void Add(List<string> selectors, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return;

        var trimmed = selector.Trim();
        if (selectors.Exists(existing => string.Equals(existing, trimmed, StringComparison.Ordinal)))
            return;

        selectors.Add(trimmed);
    }

    [GeneratedRegex(@"^(svg|path|use|circle|rect|polygon|polyline|line|g|i)(:nth-of-type\(\d+\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex GraphicPart();

    [GeneratedRegex(@"^(span|i)(:nth-of-type\(\d+\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex InlineWrapperPart();

    [GeneratedRegex(@"^(button|a|summary|label|input|select|textarea)(:nth-of-type\(\d+\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex ControlPart();
}
