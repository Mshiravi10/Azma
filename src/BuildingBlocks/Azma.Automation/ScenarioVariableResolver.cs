using System.Text.RegularExpressions;

namespace Azma.Automation;

/// <summary>Resolves {{variable}} placeholders immediately before replay.</summary>
public static partial class ScenarioVariableResolver
{
    public static RecordedScenarioDefinition Apply(
        RecordedScenarioDefinition definition,
        AutomationRunConfiguration? configuration)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in definition.Variables ?? new Dictionary<string, string>())
            variables[pair.Key] = pair.Value;

        var configuredBase = First(configuration?.Frontend, configuration?.Gateway);
        if (!string.IsNullOrWhiteSpace(configuredBase))
            variables["baseUrl"] = configuredBase!.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configuration?.Frontend))
            variables["frontend"] = configuration.Frontend!.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configuration?.Gateway))
            variables["gateway"] = configuration.Gateway!.TrimEnd('/');

        string? Resolve(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return Placeholder().Replace(value, match =>
                variables.TryGetValue(match.Groups[1].Value, out var replacement)
                    ? replacement
                    : match.Value);
        }

        RecordedScenarioAction ResolveAction(RecordedScenarioAction action) => action with
        {
            Url = Resolve(action.Url),
            Selector = Resolve(action.Selector),
            Text = Resolve(action.Text),
            Value = Resolve(action.Value),
            Label = Resolve(action.Label),
            SelectorAlternates = action.SelectorAlternates?.Select(x => Resolve(x) ?? x).ToList(),
        };

        RecordedExpectation ResolveExpectation(RecordedExpectation expectation) => expectation with
        {
            Selector = Resolve(expectation.Selector),
            Text = Resolve(expectation.Text),
            UrlPattern = Resolve(expectation.UrlPattern),
            Method = Resolve(expectation.Method),
            ExpectedValue = Resolve(expectation.ExpectedValue),
            BodyContains = Resolve(expectation.BodyContains),
            Label = Resolve(expectation.Label),
            SelectorAlternates = expectation.SelectorAlternates?.Select(x => Resolve(x) ?? x).ToList(),
        };

        RecordedNetworkCapture ResolveNetwork(RecordedNetworkCapture capture) => capture with
        {
            UrlPattern = Resolve(capture.UrlPattern) ?? capture.UrlPattern,
        };

        return definition with
        {
            StartUrl = Resolve(definition.StartUrl),
            Actions = (definition.Actions ?? Array.Empty<RecordedScenarioAction>())
                .Select(ResolveAction).ToList(),
            Expectations = (definition.Expectations ?? Array.Empty<RecordedExpectation>())
                .Select(ResolveExpectation).ToList(),
            NetworkCaptures = (definition.NetworkCaptures ?? Array.Empty<RecordedNetworkCapture>())
                .Select(ResolveNetwork).ToList(),
            Steps = definition.Steps?.Select(step => step with
            {
                Title = Resolve(step.Title),
                ActionText = Resolve(step.ActionText),
                ExpectedResult = Resolve(step.ExpectedResult),
                Actions = (step.Actions ?? Array.Empty<RecordedScenarioAction>())
                    .Select(ResolveAction).ToList(),
                Assertions = (step.Assertions ?? Array.Empty<RecordedExpectation>())
                    .Select(ResolveExpectation).ToList(),
                NetworkCaptures = step.NetworkCaptures?.Select(ResolveNetwork).ToList(),
            }).ToList(),
        };
    }

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}")]
    private static partial Regex Placeholder();
}
