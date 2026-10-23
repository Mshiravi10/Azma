using System.Diagnostics;
using System.Text.Json;

namespace Azma.Automation.Api;

public sealed class ScenarioRecordingRunner
{
    private readonly ILogger<ScenarioRecordingRunner> _logger;

    public ScenarioRecordingRunner(ILogger<ScenarioRecordingRunner> logger)
    {
        _logger = logger;
    }

    public async Task<AutomationRunResponse> RunAsync(AutomationRunRequest request, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var stepResults = new List<AutomationStepResult>();
        var networkResults = new List<AutomationNetworkResult>();
        var networkLock = new object();
        var configuredTimeout = request.Configuration?.TimeoutSeconds;
        var timeoutSeconds = configuredTimeout ?? request.Definition.TimeoutSeconds ?? 60;
        if (configuredTimeout is null)
        {
            foreach (var prerequisite in request.Prerequisites ?? Array.Empty<RecordedScenarioDefinition>())
                timeoutSeconds += prerequisite.TimeoutSeconds ?? 60;
        }
        var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 600));
        var timeoutMessage = $"Scenario exceeded the configured timeout of {timeout.TotalSeconds:0} seconds.";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            using var playwright = await Playwright.CreateAsync().WaitAsync(cts.Token);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = request.Configuration?.Headless ?? true,
            }).WaitAsync(cts.Token);

            var context = await browser.NewContextAsync().WaitAsync(cts.Token);
            var page = await context.NewPageAsync().WaitAsync(cts.Token);
            page.Response += async (_, response) =>
            {
                try
                {
                    string? body = null;
                    try
                    {
                        var contentType = response.Headers.TryGetValue("content-type", out var header)
                            ? header
                            : null;
                        if (contentType is null ||
                            contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                            contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
                        {
                            body = await response.TextAsync();
                            if (body.Length > 4000)
                                body = body[..4000];
                            body = RecordedScenarioValidator.MaskSensitiveText(body);
                        }
                    }
                    catch
                    {
                        body = null;
                    }

                    lock (networkLock)
                    {
                        networkResults.Add(new AutomationNetworkResult(
                            response.Request.Method,
                            response.Url,
                            response.Status,
                            false,
                            null,
                            ResponseBody: body));
                    }
                    await Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to capture automation network response.");
                }
            };

            var stepOrder = 0;
            foreach (var prerequisite in request.Prerequisites ?? Array.Empty<RecordedScenarioDefinition>())
            {
                var prerequisiteFailure = await RunDefinitionOnPageAsync(
                    page, prerequisite, request.Configuration, cts, ct, stopwatch,
                    stepResults, networkResults, networkLock, stepOrder, timeoutMessage);
                stepOrder = prerequisiteFailure.StepOrder;
                if (prerequisiteFailure.Response is not null)
                    return prerequisiteFailure.Response;
            }

            var mainFailure = await RunDefinitionOnPageAsync(
                page, request.Definition, request.Configuration, cts, ct, stopwatch,
                stepResults, networkResults, networkLock, stepOrder, timeoutMessage);
            if (mainFailure.Response is not null)
                return mainFailure.Response;

            stopwatch.Stop();
            List<AutomationNetworkResult> finalNetwork;
            lock (networkLock)
                finalNetwork = networkResults.ToList();
            var recordedDefinitions = (request.Prerequisites ?? Array.Empty<RecordedScenarioDefinition>())
                .Append(request.Definition)
                .Select(definition => ScenarioVariableResolver.Apply(definition, request.Configuration))
                .ToList();
            finalNetwork = MarkRecordedNetworkCaptures(finalNetwork, recordedDefinitions).ToList();

            return new AutomationRunResponse(
                true,
                AutomationFailureKind.None,
                null,
                stopwatch.ElapsedMilliseconds,
                stepResults,
                finalNetwork);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed(stopwatch, stepResults, networkResults, AutomationFailureKind.Timeout, timeoutMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scenario recording runner failed.");
            return Failed(stopwatch, stepResults, networkResults, AutomationFailureKind.RunnerFailure, ex.Message);
        }
    }

    private static async Task<(AutomationRunResponse? Response, int StepOrder)> RunDefinitionOnPageAsync(
        IPage page,
        RecordedScenarioDefinition definition,
        AutomationRunConfiguration? configuration,
        CancellationTokenSource cts,
        CancellationToken ct,
        Stopwatch stopwatch,
        List<AutomationStepResult> stepResults,
        List<AutomationNetworkResult> networkResults,
        object networkLock,
        int stepOrder,
        string timeoutMessage)
    {
        definition = ScenarioVariableResolver.Apply(definition, configuration);

        string Present(string? label)
        {
            if (string.IsNullOrWhiteSpace(definition.Name))
                return label ?? string.Empty;
            if (string.IsNullOrWhiteSpace(label))
                return definition.Name;
            return $"{definition.Name}: {label}";
        }

        string PresentError(string message) =>
            string.IsNullOrWhiteSpace(definition.Name) ? message : $"{definition.Name}: {message}";

        var blocks = BuildExecutionBlocks(definition);
        var firstAction = blocks.SelectMany(block => block.Actions).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(definition.StartUrl) &&
            firstAction?.Type != RecordedActionType.Navigate)
        {
            var startNavigation = new RecordedScenarioAction(
                RecordedActionType.Navigate,
                definition.StartUrl,
                Label: Present("Start URL"));
            blocks.Insert(0, new ExecutionBlock("Start URL", [startNavigation], [], true));
        }

        foreach (var block in blocks)
        {
            int networkStartIndex;
            lock (networkLock)
                networkStartIndex = networkResults.Count;

            List<AutomationNetworkResult> NetworkSnapshot()
            {
                lock (networkLock)
                {
                    return block.LimitNetworkToBlock
                        ? networkResults.Skip(networkStartIndex).ToList()
                        : networkResults.ToList();
                }
            }

            foreach (var action in block.Actions)
            {
                stepOrder++;
                var actionStopwatch = Stopwatch.StartNew();
                var label = PresentBlock(block.Label, action.Label, Present);
                try
                {
                    await ExecuteActionAsync(page, action, configuration, cts.Token);
                    actionStopwatch.Stop();
                    stepResults.Add(new AutomationStepResult(
                        stepOrder, $"Action:{action.Type}", true, label, null,
                        actionStopwatch.ElapsedMilliseconds, page.Url));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    actionStopwatch.Stop();
                    var evidence = await CaptureFailureEvidenceAsync(page);
                    stepResults.Add(new AutomationStepResult(
                        stepOrder, $"Action:{action.Type}", false, label,
                        PresentError(timeoutMessage), actionStopwatch.ElapsedMilliseconds,
                        evidence.PageUrl, evidence.ScreenshotBase64));
                    return (Failed(stopwatch, stepResults, networkResults,
                        AutomationFailureKind.Timeout, PresentError(timeoutMessage)), stepOrder);
                }
                catch (PlaywrightException ex) when (IsLocatorFailure(action, ex))
                {
                    actionStopwatch.Stop();
                    var evidence = await CaptureFailureEvidenceAsync(page);
                    stepResults.Add(new AutomationStepResult(
                        stepOrder, $"Action:{action.Type}", false, label,
                        PresentError(ex.Message), actionStopwatch.ElapsedMilliseconds,
                        evidence.PageUrl, evidence.ScreenshotBase64));
                    return (Failed(stopwatch, stepResults, networkResults,
                        AutomationFailureKind.LocatorFailure, PresentError(ex.Message)), stepOrder);
                }
                catch (Exception ex)
                {
                    actionStopwatch.Stop();
                    var evidence = await CaptureFailureEvidenceAsync(page);
                    stepResults.Add(new AutomationStepResult(
                        stepOrder, $"Action:{action.Type}", false, label,
                        PresentError(ex.Message), actionStopwatch.ElapsedMilliseconds,
                        evidence.PageUrl, evidence.ScreenshotBase64));
                    return (Failed(stopwatch, stepResults, networkResults,
                        AutomationFailureKind.FrontendFailure, PresentError(ex.Message)), stepOrder);
                }
            }

            foreach (var expectation in block.Assertions)
            {
                stepOrder++;
                var expectationStopwatch = Stopwatch.StartNew();
                var label = PresentBlock(block.Label, expectation.Label, Present);
                try
                {
                    await ExecuteExpectationWithNetworkPollingAsync(
                        page, expectation, NetworkSnapshot, cts.Token);
                    expectationStopwatch.Stop();
                    stepResults.Add(new AutomationStepResult(
                        stepOrder, $"Expectation:{expectation.Type}", true, label, null,
                        expectationStopwatch.ElapsedMilliseconds, page.Url));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    expectationStopwatch.Stop();
                    var evidence = await CaptureFailureEvidenceAsync(page);
                    stepResults.Add(new AutomationStepResult(
                        stepOrder, $"Expectation:{expectation.Type}", false, label,
                        PresentError(timeoutMessage), expectationStopwatch.ElapsedMilliseconds,
                        evidence.PageUrl, evidence.ScreenshotBase64));
                    return (Failed(stopwatch, stepResults, networkResults,
                        AutomationFailureKind.Timeout, PresentError(timeoutMessage)), stepOrder);
                }
                catch (Exception ex)
                {
                    expectationStopwatch.Stop();
                    var evidence = await CaptureFailureEvidenceAsync(page);
                    stepResults.Add(new AutomationStepResult(
                        stepOrder, $"Expectation:{expectation.Type}", false, label,
                        PresentError(ex.Message), expectationStopwatch.ElapsedMilliseconds,
                        evidence.PageUrl, evidence.ScreenshotBase64));

                    var kind = expectation.Type.ToString().StartsWith("Api", StringComparison.Ordinal)
                        ? AutomationFailureKind.ApiFailure
                        : AutomationFailureKind.AssertionFailure;
                    List<AutomationNetworkResult> networkSnapshot;
                    lock (networkLock)
                        networkSnapshot = networkResults.ToList();
                    return (Failed(stopwatch, stepResults,
                        MarkNetworkExpectations(networkSnapshot, expectation), kind,
                        PresentError(ex.Message)), stepOrder);
                }
            }
        }

        return (null, stepOrder);
    }

    private static List<ExecutionBlock> BuildExecutionBlocks(RecordedScenarioDefinition definition)
    {
        if (definition.Steps is { Count: > 0 })
        {
            return definition.Steps.Select(step => new ExecutionBlock(
                step.Title ?? step.ActionText,
                OAuthReplay.Prepare(step.Actions),
                step.Assertions ?? Array.Empty<RecordedExpectation>(),
                true)).ToList();
        }

        var blocks = OAuthReplay.Prepare(definition.Actions)
            .Select(action => new ExecutionBlock(action.Label, [action], [], false))
            .ToList();
        blocks.AddRange((definition.Expectations ?? Array.Empty<RecordedExpectation>())
            .Select(expectation => new ExecutionBlock(expectation.Label, [], [expectation], false)));
        return blocks;
    }

    private static string PresentBlock(
        string? blockLabel,
        string? itemLabel,
        Func<string?, string> present)
    {
        var label = string.IsNullOrWhiteSpace(blockLabel)
            ? itemLabel
            : string.IsNullOrWhiteSpace(itemLabel) || string.Equals(blockLabel, itemLabel, StringComparison.Ordinal)
                ? blockLabel
                : $"{blockLabel} — {itemLabel}";
        return present(label);
    }

    private static async Task<(string? PageUrl, string? ScreenshotBase64)> CaptureFailureEvidenceAsync(IPage page)
    {
        try
        {
            var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Type = ScreenshotType.Jpeg,
                    Quality = 55,
                    FullPage = false,
                })
                .WaitAsync(TimeSpan.FromSeconds(3));
            return (page.Url, Convert.ToBase64String(screenshot));
        }
        catch
        {
            return (page.Url, null);
        }
    }

    private sealed record ExecutionBlock(
        string? Label,
        IReadOnlyList<RecordedScenarioAction> Actions,
        IReadOnlyList<RecordedExpectation> Assertions,
        bool LimitNetworkToBlock);

    private static async Task ExecuteActionAsync(
        IPage page,
        RecordedScenarioAction action,
        AutomationRunConfiguration? configuration,
        CancellationToken ct)
    {
        switch (action.Type)
        {
            case RecordedActionType.Navigate:
                await ReplayNavigationAsync(page, action, configuration, ct);
                break;
            case RecordedActionType.Click:
                await ExecuteWithSelectorFallbackAsync(
                    page,
                    action,
                    (locator, timeout) => locator.ClickAsync(new LocatorClickOptions { Timeout = timeout }),
                    ct);
                break;
            case RecordedActionType.Fill:
                await ExecuteWithSelectorFallbackAsync(
                    page,
                    action,
                    (locator, timeout) => locator.FillAsync(
                        action.Value ?? string.Empty,
                        new LocatorFillOptions { Timeout = timeout }),
                    ct);
                break;
            case RecordedActionType.Select:
                await ExecuteWithSelectorFallbackAsync(
                    page,
                    action,
                    (locator, timeout) => locator.SelectOptionAsync(
                        action.Value!,
                        new LocatorSelectOptionOptions { Timeout = timeout }),
                    ct);
                break;
            case RecordedActionType.Submit:
                await SubmitRecordedFormAsync(page, action, ct);
                break;
            case RecordedActionType.Wait:
                await Task.Delay(action.DelayMs ?? 0, ct);
                break;
        }
    }

    private static async Task ReplayNavigationAsync(
        IPage page,
        RecordedScenarioAction action,
        AutomationRunConfiguration? configuration,
        CancellationToken ct)
    {
        var url = ResolveUrl(action.Url!, configuration);
        if (OAuthReplay.IsAuthorizationRequest(url))
        {
            await WaitForPasswordFieldAsync(page, ct);
            return;
        }

        if (OAuthReplay.IsCallback(url))
        {
            await WaitForCompletedLoginAsync(page, ct);
            return;
        }

        await page.GotoAsync(url).WaitAsync(ct);
    }

    private static async Task WaitForPasswordFieldAsync(IPage page, CancellationToken ct)
    {
        try
        {
            await page.Locator("input[name='password'], input[type='password']")
                .First
                .WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 15_000,
                })
                .WaitAsync(ct);
        }
        catch (PlaywrightException)
        {
        }
    }

    private static async Task SubmitRecordedFormAsync(
        IPage page,
        RecordedScenarioAction action,
        CancellationToken ct)
    {
        if (!OAuthReplay.IsLoginUrl(page.Url))
            return;

        var password = page.Locator("input[name='password'], input[type='password']").First;
        try
        {
            if (await password.IsVisibleAsync())
            {
                await password.PressAsync("Enter", new LocatorPressOptions { Timeout = 5_000 }).WaitAsync(ct);
                return;
            }
        }
        catch (PlaywrightException)
        {
        }

        await ExecuteWithSelectorFallbackAsync(
            page,
            action,
            (locator, timeout) => locator.PressAsync("Enter", new LocatorPressOptions { Timeout = timeout }),
            ct);
    }

    private static async Task WaitForCompletedLoginAsync(IPage page, CancellationToken ct)
    {
        try
        {
            await page.WaitForURLAsync(
                    url => !OAuthReplay.IsLoginUrl(url) && !OAuthReplay.IsCallback(url),
                    new PageWaitForURLOptions { Timeout = 20_000 })
                .WaitAsync(ct);
        }
        catch (PlaywrightException)
        {
        }

        try
        {
            await page.WaitForLoadStateAsync(
                    LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 10_000 })
                .WaitAsync(ct);
        }
        catch (PlaywrightException)
        {
        }
    }

    private static async Task ExecuteWithSelectorFallbackAsync(
        IPage page,
        RecordedScenarioAction action,
        Func<ILocator, float, Task> execute,
        CancellationToken ct)
    {
        var selectors = SelectorsFor(action).ToList();
        if (selectors.Count == 0)
            throw new InvalidOperationException($"{action.Type} requires at least one selector.");

        PlaywrightException? lastPlaywrightException = null;
        for (var index = 0; index < selectors.Count; index++)
        {
            var timeout = index == 0 ? 30_000f : 4_000f;
            try
            {
                await execute(page.Locator(selectors[index]), timeout).WaitAsync(ct);
                return;
            }
            catch (PlaywrightException ex)
            {
                lastPlaywrightException = ex;
            }
        }

        throw lastPlaywrightException ?? new PlaywrightException($"No selector matched for {action.Type}.");
    }

    private static async Task ExecuteExpectationAsync(
        IPage page,
        RecordedExpectation expectation,
        IReadOnlyList<AutomationNetworkResult> network,
        CancellationToken ct)
    {
        switch (expectation.Type)
        {
            case RecordedExpectationType.VisibleText:
                await page.GetByText(expectation.Text!).WaitForAsync().WaitAsync(ct);
                break;
            case RecordedExpectationType.ElementVisible:
                await WaitForElementWithSelectorFallbackAsync(page, expectation, ct);
                break;
            case RecordedExpectationType.UrlMatches:
                if (!Matches(page.Url, expectation.UrlPattern!))
                    throw new InvalidOperationException($"Current URL '{page.Url}' does not match '{expectation.UrlPattern}'.");
                break;
            case RecordedExpectationType.ApiStatus:
                if (!network.Any(x => Matches(x.Url, expectation.UrlPattern!) &&
                                      MethodMatches(x.Method, expectation.Method) &&
                                      x.StatusCode == expectation.StatusCode))
                    throw new InvalidOperationException($"No API response matched {expectation.Method ?? "*"} {expectation.UrlPattern} with status {expectation.StatusCode}.");
                break;
            case RecordedExpectationType.ApiBodyContains:
                if (!network.Any(x => Matches(x.Url, expectation.UrlPattern!) &&
                                      MethodMatches(x.Method, expectation.Method) &&
                                      (x.ResponseBody?.Contains(expectation.BodyContains!, StringComparison.OrdinalIgnoreCase) ?? false)))
                    throw new InvalidOperationException($"No API response body matched {expectation.Method ?? "*"} {expectation.UrlPattern} containing '{expectation.BodyContains}'.");
                break;
            case RecordedExpectationType.ApiJsonPathEquals:
                if (!network.Any(x => Matches(x.Url, expectation.UrlPattern!) &&
                                      MethodMatches(x.Method, expectation.Method) &&
                                      JsonPathValueEquals(x.ResponseBody, expectation.JsonPath!, expectation.ExpectedValue)))
                    throw new InvalidOperationException($"No API JSON response matched {expectation.Method ?? "*"} {expectation.UrlPattern} at {expectation.JsonPath} = '{expectation.ExpectedValue}'.");
                break;
            default:
                await Task.CompletedTask;
                break;
        }

        ct.ThrowIfCancellationRequested();
    }

    private static async Task ExecuteExpectationWithNetworkPollingAsync(
        IPage page,
        RecordedExpectation expectation,
        Func<IReadOnlyList<AutomationNetworkResult>> networkSnapshot,
        CancellationToken ct)
    {
        var isApi = expectation.Type is RecordedExpectationType.ApiStatus or
            RecordedExpectationType.ApiBodyContains or
            RecordedExpectationType.ApiJsonPathEquals;
        if (!isApi)
        {
            await ExecuteExpectationAsync(page, expectation, networkSnapshot(), ct);
            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(8);
        InvalidOperationException? lastError = null;
        do
        {
            try
            {
                await ExecuteExpectationAsync(page, expectation, networkSnapshot(), ct);
                return;
            }
            catch (InvalidOperationException ex)
            {
                lastError = ex;
            }

            await Task.Delay(200, ct);
        }
        while (DateTime.UtcNow < deadline);

        throw lastError ?? new InvalidOperationException("No API response matched the expectation.");
    }

    private static async Task WaitForElementWithSelectorFallbackAsync(
        IPage page,
        RecordedExpectation expectation,
        CancellationToken ct)
    {
        var selectors = ClickTargetSelectors
            .Expand(expectation.Selector, expectation.SelectorAlternates)
            .ToList();
        PlaywrightException? lastError = null;
        for (var index = 0; index < selectors.Count; index++)
        {
            try
            {
                await page.Locator(selectors[index]).WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = index == 0 ? 8_000 : 3_000,
                }).WaitAsync(ct);
                return;
            }
            catch (PlaywrightException ex)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new PlaywrightException("No selector matched the expected element.");
    }

    private static string ResolveUrl(string url, AutomationRunConfiguration? configuration)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        var frontend = configuration?.Frontend?.TrimEnd('/');
        var gateway = configuration?.Gateway?.TrimEnd('/');
        var baseUrl = !string.IsNullOrWhiteSpace(frontend)
            ? frontend
            : gateway;

        if (string.IsNullOrWhiteSpace(baseUrl))
            return url;

        return $"{baseUrl}/{url.TrimStart('/')}";
    }

    private static bool Matches(string value, string pattern)
    {
        if (pattern == "*")
            return true;
        if (pattern.Contains('*', StringComparison.Ordinal))
        {
            var parts = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
            var index = 0;
            foreach (var part in parts)
            {
                var found = value.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                    return false;
                index = found + part.Length;
            }
            return true;
        }

        return value.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MethodMatches(string actual, string? expected) =>
        string.IsNullOrWhiteSpace(expected) ||
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool JsonPathValueEquals(string? body, string jsonPath, string? expected)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(jsonPath))
            return false;

        try
        {
            using var document = JsonDocument.Parse(body);
            var current = document.RootElement;
            var path = jsonPath.Trim();
            if (path.StartsWith("$.", StringComparison.Ordinal))
                path = path[2..];
            else if (path.StartsWith("$", StringComparison.Ordinal))
                path = path[1..].TrimStart('.');

            foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                    return false;
            }

            var actual = current.ValueKind == JsonValueKind.String
                ? current.GetString()
                : current.GetRawText();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> SelectorsFor(RecordedScenarioAction action) =>
        ClickTargetSelectors.Expand(action.Selector, action.SelectorAlternates);

    private static bool IsLocatorFailure(RecordedScenarioAction action, PlaywrightException ex) =>
        SelectorsFor(action).Any(selector => ex.Message.Contains(selector, StringComparison.OrdinalIgnoreCase)) ||
        ex.Message.Contains("locator", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<AutomationNetworkResult> MarkNetworkExpectations(
        IReadOnlyList<AutomationNetworkResult> network,
        RecordedExpectation expectation) =>
        network
            .Select(x => x with
            {
                MatchedExpectation = Matches(x.Url, expectation.UrlPattern ?? "*") &&
                                     MethodMatches(x.Method, expectation.Method)
            })
            .ToList();

    private static IReadOnlyList<AutomationNetworkResult> MarkRecordedNetworkCaptures(
        IReadOnlyList<AutomationNetworkResult> network,
        IReadOnlyList<RecordedScenarioDefinition> definitions)
    {
        var captures = definitions
            .SelectMany(definition =>
                (definition.NetworkCaptures ?? Array.Empty<RecordedNetworkCapture>())
                .Concat((definition.Steps ?? Array.Empty<RecordedScenarioStep>())
                    .SelectMany(step => step.NetworkCaptures ?? Array.Empty<RecordedNetworkCapture>())))
            .ToList();
        if (captures.Count == 0)
            return network;

        return network.Select(result => result with
        {
            MatchedExpectation = result.MatchedExpectation || captures.Any(capture =>
                Matches(result.Url, capture.UrlPattern) &&
                MethodMatches(result.Method, capture.Method) &&
                (capture.ExpectedStatus is null || capture.ExpectedStatus == result.StatusCode)),
        }).ToList();
    }

    private static AutomationRunResponse Failed(
        Stopwatch stopwatch,
        IReadOnlyList<AutomationStepResult> steps,
        IReadOnlyList<AutomationNetworkResult> network,
        AutomationFailureKind kind,
        string? message)
    {
        stopwatch.Stop();
        return new AutomationRunResponse(false, kind, message, stopwatch.ElapsedMilliseconds, steps, network);
    }
}
