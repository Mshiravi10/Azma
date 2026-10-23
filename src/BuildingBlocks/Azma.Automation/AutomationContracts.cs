namespace Azma.Automation;

public enum AutomationEngine
{
    None = 0,
    ScenarioRecording = 2,
}

public enum AutomationStatus
{
    None = 0,
    Draft = 1,
    Validated = 2,
    Published = 3,
    Broken = 4,
}

public enum RecordedActionType
{
    Navigate = 0,
    Click = 1,
    Fill = 2,
    Select = 3,
    Submit = 4,
    Wait = 5,
}

public enum RecordedExpectationType
{
    VisibleText = 0,
    ElementVisible = 1,
    UrlMatches = 2,
    ApiStatus = 3,
    ApiJsonPathEquals = 4,
    ApiBodyContains = 5,
}

public enum AutomationFailureKind
{
    None = 0,
    FrontendFailure = 1,
    ApiFailure = 2,
    AssertionFailure = 3,
    Timeout = 4,
    LocatorFailure = 5,
    ValidationFailure = 6,
    RunnerFailure = 7,
}

public sealed record RecordedScenarioDefinition(
    string? Name,
    string? StartUrl,
    IReadOnlyList<RecordedScenarioAction> Actions,
    IReadOnlyList<RecordedNetworkCapture> NetworkCaptures,
    IReadOnlyList<RecordedExpectation> Expectations,
    IReadOnlyDictionary<string, string>? Variables = null,
    int? TimeoutSeconds = null,
    IReadOnlyList<RecordedScenarioStep>? Steps = null,
    int SchemaVersion = 1)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static RecordedScenarioDefinition? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<RecordedScenarioDefinition>(json, JsonOptions);
    }
}

public sealed record RecordedScenarioAction(
    RecordedActionType Type,
    string? Url = null,
    string? Selector = null,
    string? Text = null,
    string? Value = null,
    int? DelayMs = null,
    string? Label = null,
    IReadOnlyList<string>? SelectorAlternates = null,
    bool Sensitive = false,
    string? SelectorStrategy = null,
    int? SelectorConfidence = null);

/// <summary>
/// A scenario-studio step keeps the human test-case wording and its executable
/// automation together. Multiple low-level actions may intentionally be grouped
/// into one business step; assertions execute immediately after those actions.
/// </summary>
public sealed record RecordedScenarioStep(
    string Id,
    string? Title,
    string? ActionText,
    string? ExpectedResult,
    IReadOnlyList<RecordedScenarioAction> Actions,
    IReadOnlyList<RecordedExpectation> Assertions,
    IReadOnlyList<RecordedNetworkCapture>? NetworkCaptures = null,
    RecordedStepEvidence? Evidence = null);

public sealed record RecordedStepEvidence(
    string? Url = null,
    string? PageTitle = null,
    DateTime? CapturedAtUtc = null);

public sealed record RecordedNetworkCapture(
    string Method,
    string UrlPattern,
    int? ExpectedStatus = null,
    long? DurationMs = null,
    string? RequestSummary = null,
    string? ResponseSummary = null);

public sealed record RecordedExpectation(
    RecordedExpectationType Type,
    string? Selector = null,
    string? Text = null,
    string? UrlPattern = null,
    string? Method = null,
    int? StatusCode = null,
    string? JsonPath = null,
    string? ExpectedValue = null,
    string? BodyContains = null,
    string? Label = null,
    IReadOnlyList<string>? SelectorAlternates = null,
    string? SelectorStrategy = null,
    int? SelectorConfidence = null);

public sealed record AutomationRunConfiguration(
    string? Gateway = null,
    string? Frontend = null,
    bool Headless = true,
    int? TimeoutSeconds = null);

public sealed record AutomationRunRequest(
    Guid? WorkspaceId,
    Guid? ProjectId,
    Guid? TestCaseId,
    string? TestCaseCode,
    RecordedScenarioDefinition Definition,
    AutomationRunConfiguration? Configuration = null,
    IReadOnlyList<RecordedScenarioDefinition>? Prerequisites = null);

public sealed record AutomationValidationRequest(RecordedScenarioDefinition Definition);

public sealed record AutomationValidationResponse(
    bool IsValid,
    IReadOnlyList<string> Errors,
    RecordedScenarioDefinition? SanitizedDefinition = null);

public sealed record AutomationRunResponse(
    bool Success,
    AutomationFailureKind FailureKind,
    string? ErrorMessage,
    long DurationMs,
    IReadOnlyList<AutomationStepResult> Steps,
    IReadOnlyList<AutomationNetworkResult> Network);

public sealed record AutomationStepResult(
    int Order,
    string Kind,
    bool Success,
    string? Label,
    string? ErrorMessage,
    long DurationMs,
    string? PageUrl = null,
    string? ScreenshotBase64 = null);

public sealed record AutomationNetworkResult(
    string Method,
    string Url,
    int StatusCode,
    bool MatchedExpectation,
    long? DurationMs,
    string? ErrorMessage = null,
    string? ResponseBody = null);
