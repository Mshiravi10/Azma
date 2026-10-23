namespace Azma.Automation;

public static class RecordedScenarioValidator
{
    private static readonly string[] SensitiveKeys =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "authorization",
        "cookie",
        "refresh",
    ];

    public static AutomationValidationResponse Validate(RecordedScenarioDefinition? definition)
    {
        var errors = new List<string>();
        if (definition is null)
        {
            errors.Add("Recording definition is required.");
            return new AutomationValidationResponse(false, errors);
        }

        var actions = definition.Actions ?? Array.Empty<RecordedScenarioAction>();
        var expectations = definition.Expectations ?? Array.Empty<RecordedExpectation>();
        var network = definition.NetworkCaptures ?? Array.Empty<RecordedNetworkCapture>();
        var steps = definition.Steps ?? Array.Empty<RecordedScenarioStep>();

        var stepActions = steps.Sum(step => step.Actions?.Count ?? 0);
        var stepAssertions = steps.Sum(step => step.Assertions?.Count ?? 0);
        var stepNetwork = steps.Sum(step => step.NetworkCaptures?.Count ?? 0);
        if (actions.Count == 0 && expectations.Count == 0 && network.Count == 0 &&
            stepActions == 0 && stepAssertions == 0 && stepNetwork == 0)
            errors.Add("Recording must contain at least one action, network capture, or expectation.");

        if (steps.Count > 1000)
            errors.Add("Recording cannot contain more than 1000 scenario steps.");

        var stepIds = new HashSet<string>(StringComparer.Ordinal);
        for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            var step = steps[stepIndex];
            if (string.IsNullOrWhiteSpace(step.Id))
                errors.Add($"Scenario step {stepIndex + 1} requires an id.");
            else if (!stepIds.Add(step.Id.Trim()))
                errors.Add($"Scenario step id '{step.Id}' must be unique.");

            if (step.Id?.Length > 200)
                errors.Add($"Scenario step {stepIndex + 1} id cannot exceed 200 characters.");
            if (step.Title?.Length > 300)
                errors.Add($"Scenario step {stepIndex + 1} title cannot exceed 300 characters.");
            if (step.ActionText?.Length > 2000)
                errors.Add($"Scenario step {stepIndex + 1} action text cannot exceed 2000 characters.");
            if (step.ExpectedResult?.Length > 2000)
                errors.Add($"Scenario step {stepIndex + 1} expected result cannot exceed 2000 characters.");

            var nestedActions = step.Actions ?? Array.Empty<RecordedScenarioAction>();
            for (var actionIndex = 0; actionIndex < nestedActions.Count; actionIndex++)
                ValidateAction(nestedActions[actionIndex], actionIndex + 1, errors, $"Scenario step {stepIndex + 1} action");

            var nestedAssertions = step.Assertions ?? Array.Empty<RecordedExpectation>();
            for (var assertionIndex = 0; assertionIndex < nestedAssertions.Count; assertionIndex++)
                ValidateExpectation(nestedAssertions[assertionIndex], assertionIndex + 1, errors, $"Scenario step {stepIndex + 1} assertion");

            var nestedNetwork = step.NetworkCaptures ?? Array.Empty<RecordedNetworkCapture>();
            for (var networkIndex = 0; networkIndex < nestedNetwork.Count; networkIndex++)
                ValidateNetwork(nestedNetwork[networkIndex], networkIndex + 1, errors, $"Scenario step {stepIndex + 1} network capture");
        }

        for (var i = 0; i < actions.Count; i++)
            ValidateAction(actions[i], i + 1, errors, "Action");

        for (var i = 0; i < expectations.Count; i++)
            ValidateExpectation(expectations[i], i + 1, errors, "Expectation");

        for (var i = 0; i < network.Count; i++)
        {
            ValidateNetwork(network[i], i + 1, errors, "Network capture");
        }

        if (definition.TimeoutSeconds is < 1 or > 600)
            errors.Add("TimeoutSeconds must be between 1 and 600.");

        return new AutomationValidationResponse(
            errors.Count == 0,
            errors,
            errors.Count == 0 ? Sanitize(definition) : null);
    }

    public static RecordedScenarioDefinition Sanitize(RecordedScenarioDefinition definition)
    {
        return definition with
        {
            Actions = (definition.Actions ?? Array.Empty<RecordedScenarioAction>())
                .Select(SanitizeAction)
                .ToList(),
            NetworkCaptures = (definition.NetworkCaptures ?? Array.Empty<RecordedNetworkCapture>())
                .Select(SanitizeNetwork)
                .ToList(),
            Expectations = (definition.Expectations ?? Array.Empty<RecordedExpectation>())
                .Select(SanitizeExpectation)
                .ToList(),
            Steps = definition.Steps?.Select(SanitizeStep).ToList(),
            Variables = definition.Variables?.ToDictionary(
                x => x.Key,
                x => IsSensitive(x.Key) ? "***" : x.Value)
        };
    }

    private static void ValidateAction(
        RecordedScenarioAction action,
        int order,
        List<string> errors,
        string prefix)
    {
        switch (action.Type)
        {
            case RecordedActionType.Navigate:
                if (string.IsNullOrWhiteSpace(action.Url))
                    errors.Add($"{prefix} {order} Navigate requires Url.");
                break;
            case RecordedActionType.Click:
            case RecordedActionType.Fill:
            case RecordedActionType.Select:
            case RecordedActionType.Submit:
                if (string.IsNullOrWhiteSpace(action.Selector))
                    errors.Add($"{prefix} {order} {action.Type} requires Selector.");
                if (action.Type is RecordedActionType.Fill or RecordedActionType.Select &&
                    string.IsNullOrWhiteSpace(action.Value))
                    errors.Add($"{prefix} {order} {action.Type} requires Value.");
                break;
            case RecordedActionType.Wait:
                if (action.DelayMs is null or < 0 or > 120000)
                    errors.Add($"{prefix} {order} Wait requires DelayMs between 0 and 120000.");
                break;
            default:
                errors.Add($"{prefix} {order} has an unsupported type.");
                break;
        }
    }

    private static void ValidateExpectation(
        RecordedExpectation expectation,
        int order,
        List<string> errors,
        string prefix)
    {
        switch (expectation.Type)
        {
            case RecordedExpectationType.VisibleText:
                if (string.IsNullOrWhiteSpace(expectation.Text))
                    errors.Add($"{prefix} {order} VisibleText requires Text.");
                break;
            case RecordedExpectationType.ElementVisible:
                if (string.IsNullOrWhiteSpace(expectation.Selector))
                    errors.Add($"{prefix} {order} ElementVisible requires Selector.");
                break;
            case RecordedExpectationType.UrlMatches:
                if (string.IsNullOrWhiteSpace(expectation.UrlPattern))
                    errors.Add($"{prefix} {order} UrlMatches requires UrlPattern.");
                break;
            case RecordedExpectationType.ApiStatus:
                if (string.IsNullOrWhiteSpace(expectation.UrlPattern))
                    errors.Add($"{prefix} {order} ApiStatus requires UrlPattern.");
                if (expectation.StatusCode is null or < 100 or > 599)
                    errors.Add($"{prefix} {order} ApiStatus requires a valid StatusCode.");
                break;
            case RecordedExpectationType.ApiJsonPathEquals:
                if (string.IsNullOrWhiteSpace(expectation.UrlPattern))
                    errors.Add($"{prefix} {order} ApiJsonPathEquals requires UrlPattern.");
                if (string.IsNullOrWhiteSpace(expectation.JsonPath))
                    errors.Add($"{prefix} {order} ApiJsonPathEquals requires JsonPath.");
                if (expectation.ExpectedValue is null)
                    errors.Add($"{prefix} {order} ApiJsonPathEquals requires ExpectedValue.");
                break;
            case RecordedExpectationType.ApiBodyContains:
                if (string.IsNullOrWhiteSpace(expectation.UrlPattern))
                    errors.Add($"{prefix} {order} ApiBodyContains requires UrlPattern.");
                if (string.IsNullOrWhiteSpace(expectation.BodyContains))
                    errors.Add($"{prefix} {order} ApiBodyContains requires BodyContains.");
                break;
            default:
                errors.Add($"{prefix} {order} has an unsupported type.");
                break;
        }
    }

    private static void ValidateNetwork(
        RecordedNetworkCapture capture,
        int order,
        List<string> errors,
        string prefix)
    {
        if (string.IsNullOrWhiteSpace(capture.Method))
            errors.Add($"{prefix} {order} requires a method.");
        if (string.IsNullOrWhiteSpace(capture.UrlPattern))
            errors.Add($"{prefix} {order} requires a URL pattern.");
    }

    public static string? MaskForDisplay(string? recordingJson)
    {
        var definition = RecordedScenarioDefinition.FromJson(recordingJson);
        if (definition is null)
            return recordingJson;

        return (definition with
        {
            Actions = (definition.Actions ?? Array.Empty<RecordedScenarioAction>())
                .Select(MaskActionForDisplay)
                .ToList(),
            Steps = definition.Steps?.Select(step => step with
            {
                Actions = (step.Actions ?? Array.Empty<RecordedScenarioAction>())
                    .Select(MaskActionForDisplay)
                    .ToList(),
            }).ToList(),
        }).ToJson();
    }

    private static RecordedScenarioAction SanitizeAction(RecordedScenarioAction action)
    {
        var alternates = action.SelectorAlternates?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return action with
        {
            SelectorAlternates = alternates is { Count: > 0 } ? alternates : null,
            SelectorConfidence = action.SelectorConfidence is null
                ? null
                : Math.Clamp(action.SelectorConfidence.Value, 0, 100),
        };
    }

    private static RecordedScenarioStep SanitizeStep(RecordedScenarioStep step) => step with
    {
        Id = step.Id.Trim(),
        Title = Normalize(step.Title),
        ActionText = Normalize(step.ActionText),
        ExpectedResult = Normalize(step.ExpectedResult),
        Actions = (step.Actions ?? Array.Empty<RecordedScenarioAction>())
            .Select(SanitizeAction).ToList(),
        Assertions = (step.Assertions ?? Array.Empty<RecordedExpectation>())
            .Select(SanitizeExpectation).ToList(),
        NetworkCaptures = step.NetworkCaptures?
            .Select(SanitizeNetwork).ToList(),
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RecordedScenarioAction MaskActionForDisplay(RecordedScenarioAction action)
    {
        if (action.Type is not (RecordedActionType.Fill or RecordedActionType.Select) ||
            string.IsNullOrEmpty(action.Value) ||
            !IsSensitiveAction(action))
            return action;

        return action with { Value = "***" };
    }

    private static bool IsSensitiveAction(RecordedScenarioAction action) =>
        action.Sensitive || IsSensitive(action.Selector) || IsSensitive(action.Label);

    private static RecordedNetworkCapture SanitizeNetwork(RecordedNetworkCapture capture) =>
        capture with
        {
            RequestSummary = MaskSensitiveText(capture.RequestSummary),
            ResponseSummary = MaskSensitiveText(capture.ResponseSummary),
        };

    private static RecordedExpectation SanitizeExpectation(RecordedExpectation expectation)
    {
        var alternates = expectation.SelectorAlternates?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return expectation with
        {
            ExpectedValue = IsSensitive(expectation.JsonPath) || IsSensitive(expectation.Label)
                ? "***"
                : expectation.ExpectedValue,
            BodyContains = MaskSensitiveText(expectation.BodyContains),
            SelectorAlternates = alternates is { Count: > 0 } ? alternates : null,
            SelectorConfidence = expectation.SelectorConfidence is null
                ? null
                : Math.Clamp(expectation.SelectorConfidence.Value, 0, 100),
        };
    }

    public static string? MaskSensitiveText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return SensitiveKeys.Any(key => value.Contains(key, StringComparison.OrdinalIgnoreCase))
            ? "***"
            : value;
    }

    private static bool IsSensitive(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        SensitiveKeys.Any(key => value.Contains(key, StringComparison.OrdinalIgnoreCase));
}
