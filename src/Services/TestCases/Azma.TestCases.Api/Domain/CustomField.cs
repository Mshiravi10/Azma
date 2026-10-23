using System.Text.Json;

namespace Azma.TestCases.Api.Domain;

public enum CustomFieldType
{
    Text = 0,
    LongText = 1,
    Number = 2,
    Date = 3,
    Boolean = 4,
    Select = 5,
    MultiSelect = 6,
    Url = 7,
}

public sealed class CustomFieldDefinition
{
    private CustomFieldDefinition() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }

    /// <summary>Stable machine identifier used as JSON key on test cases. Lowercase snake_case.</summary>
    public string Key { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public string? Description { get; private set; }
    public CustomFieldType Type { get; private set; } = CustomFieldType.Text;

    /// <summary>For Select/MultiSelect: list of allowed options (stored as JSON).</summary>
    public List<string>? Options { get; private set; }

    public bool Required { get; private set; }
    public int Position { get; private set; }
    public bool IsArchived { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; private set; }

    public static CustomFieldDefinition Create(
        Guid workspaceId, Guid projectId,
        string key, string label, string? description,
        CustomFieldType type, List<string>? options, bool required, int position)
    {
        return new CustomFieldDefinition
        {
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            Key = NormalizeKey(key),
            Label = label.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Type = type,
            Options = NormalizeOptions(type, options),
            Required = required,
            Position = position,
        };
    }

    public void Update(string label, string? description, List<string>? options, bool required, int position)
    {
        Label = label.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Options = NormalizeOptions(Type, options);
        Required = required;
        Position = position;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Archive() => IsArchived = true;

    public static string NormalizeKey(string key)
    {
        var trimmed = key.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_') sb.Append('_');
        }
        return sb.ToString();
    }

    private static List<string>? NormalizeOptions(CustomFieldType type, List<string>? opts)
    {
        if (type is not (CustomFieldType.Select or CustomFieldType.MultiSelect)) return null;
        if (opts is null) return new List<string>();
        return opts.Where(o => !string.IsNullOrWhiteSpace(o))
                   .Select(o => o.Trim())
                   .Distinct()
                   .ToList();
    }

    /// <summary>Validate and coerce a value coming from the API for this field.</summary>
    public bool TryCoerce(JsonElement input, out JsonElement coerced, out string? error)
    {
        coerced = input;
        error = null;
        if (input.ValueKind == JsonValueKind.Null || input.ValueKind == JsonValueKind.Undefined)
        {
            if (Required) { error = $"فیلد '{Label}' الزامی است."; return false; }
            coerced = default;
            return true;
        }

        switch (Type)
        {
            case CustomFieldType.Text:
            case CustomFieldType.LongText:
            case CustomFieldType.Url:
                if (input.ValueKind != JsonValueKind.String) { error = $"'{Label}' باید رشته باشد."; return false; }
                if (Required && string.IsNullOrWhiteSpace(input.GetString())) { error = $"'{Label}' نمی‌تواند خالی باشد."; return false; }
                return true;

            case CustomFieldType.Number:
                if (input.ValueKind != JsonValueKind.Number) { error = $"'{Label}' باید عدد باشد."; return false; }
                return true;

            case CustomFieldType.Boolean:
                if (input.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                { error = $"'{Label}' باید true/false باشد."; return false; }
                return true;

            case CustomFieldType.Date:
                if (input.ValueKind != JsonValueKind.String || !DateTime.TryParse(input.GetString(), out _))
                { error = $"'{Label}' باید تاریخ ISO معتبر باشد."; return false; }
                return true;

            case CustomFieldType.Select:
                if (input.ValueKind != JsonValueKind.String) { error = $"'{Label}' باید یکی از گزینه‌ها باشد."; return false; }
                if (Options is { Count: > 0 } && !Options.Contains(input.GetString() ?? ""))
                { error = $"'{Label}' خارج از گزینه‌های مجاز است."; return false; }
                return true;

            case CustomFieldType.MultiSelect:
                if (input.ValueKind != JsonValueKind.Array) { error = $"'{Label}' باید لیستی از گزینه‌ها باشد."; return false; }
                foreach (var item in input.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) { error = $"عناصر '{Label}' باید رشته باشند."; return false; }
                    if (Options is { Count: > 0 } && !Options.Contains(item.GetString() ?? ""))
                    { error = $"مقدار '{item}' خارج از گزینه‌های '{Label}' است."; return false; }
                }
                return true;

            default:
                return true;
        }
    }
}
