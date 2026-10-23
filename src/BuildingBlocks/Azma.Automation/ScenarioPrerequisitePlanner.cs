namespace Azma.Automation;

public static class ScenarioPrerequisitePlanner
{
    public const int MaxDepth = 5;

    public sealed record ScenarioLink(
        Guid Id,
        Guid ProjectId,
        string Title,
        Guid? PrerequisiteTestCaseId,
        string? RecordingJson,
        bool IsDeleted);

    public sealed record Plan(
        bool Succeeded,
        string? Error,
        IReadOnlyList<RecordedScenarioDefinition> Definitions)
    {
        public static Plan Ok(IReadOnlyList<RecordedScenarioDefinition> definitions) =>
            new(true, null, definitions);

        public static Plan Fail(string error) =>
            new(false, error, Array.Empty<RecordedScenarioDefinition>());
    }

    public static Plan Create(
        Guid scenarioId,
        Guid projectId,
        Guid? prerequisiteTestCaseId,
        Func<Guid, ScenarioLink?> resolve)
    {
        if (prerequisiteTestCaseId is null)
            return Plan.Ok(Array.Empty<RecordedScenarioDefinition>());

        var chain = new List<Guid>();
        var seen = new HashSet<Guid> { scenarioId };
        var current = prerequisiteTestCaseId;
        while (current is Guid id)
        {
            if (!seen.Add(id))
                return Plan.Fail("پیش‌نیازها حلقه ساخته‌اند. یک سناریو نمی‌تواند، مستقیم یا با واسطه، پیش‌نیاز خودش باشد.");

            if (chain.Count >= MaxDepth)
                return Plan.Fail($"زنجیره پیش‌نیاز نمی‌تواند بیشتر از {MaxDepth} سناریو باشد.");

            var link = resolve(id);
            if (link is null || link.IsDeleted)
                return Plan.Fail("سناریوی پیش‌نیاز پیدا نشد.");

            if (link.ProjectId != projectId)
                return Plan.Fail("پیش‌نیاز باید سناریویی از همین پروژه باشد.");

            if (string.IsNullOrWhiteSpace(link.RecordingJson))
                return Plan.Fail($"سناریوی «{link.Title}» هنوز ضبط نشده و نمی‌تواند پیش‌نیاز باشد.");

            chain.Add(id);
            current = link.PrerequisiteTestCaseId;
        }

        chain.Reverse();
        var definitions = new List<RecordedScenarioDefinition>(chain.Count);
        foreach (var id in chain)
        {
            var link = resolve(id)!;
            var validation = RecordedScenarioValidator.Validate(
                RecordedScenarioDefinition.FromJson(link.RecordingJson));
            if (!validation.IsValid || validation.SanitizedDefinition is null)
            {
                return Plan.Fail(
                    $"ضبط پیش‌نیاز «{link.Title}» معتبر نیست: {string.Join("; ", validation.Errors)}");
            }

            var sanitized = validation.SanitizedDefinition;
            var name = string.IsNullOrWhiteSpace(sanitized.Name) ? link.Title : sanitized.Name;
            definitions.Add(sanitized with { Name = name });
        }

        return Plan.Ok(definitions);
    }
}
