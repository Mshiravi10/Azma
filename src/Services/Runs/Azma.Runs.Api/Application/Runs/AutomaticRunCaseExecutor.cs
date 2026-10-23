using System.Diagnostics;
using System.Text.Json;
using Azma.Automation;
using Azma.Common.Results;
using Azma.EventBus.Contracts.Runs;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Application.Runs;

public sealed record AutomaticRunCaseExecutionOptions(
    string? Gateway = null,
    string? Frontend = null,
    string CommentPrefix = "Automatic execution",
    bool RecordFailures = false);

public sealed record AutomaticRunCaseExecutionOutcome(
    RunResult Result,
    string ScenarioName,
    bool Succeeded,
    AutomationEngine AutomationEngine,
    AutomationRunResponse? AutomationRun,
    string? ErrorMessage);

public sealed class AutomaticRunCaseExecutor
{
    private readonly RunsDbContext _db;
    private readonly IAutomationApiClient _automation;
    private readonly IPublishEndpoint _bus;

    public AutomaticRunCaseExecutor(
        RunsDbContext db,
        IAutomationApiClient automation,
        IPublishEndpoint bus)
    {
        _db = db;
        _automation = automation;
        _bus = bus;
    }

    public async Task<Result<AutomaticRunCaseExecutionOutcome>> ExecuteAsync(
        TestRun run,
        RunCase runCase,
        Guid executedByUserId,
        AutomaticRunCaseExecutionOptions options,
        CancellationToken ct)
    {
        if (!runCase.HasAutomation)
            await RefreshMissingAutomationSnapshotAsync(run, runCase, ct);

        if (runCase.AutomationEngine != AutomationEngine.ScenarioRecording)
            return Result.Failure<AutomaticRunCaseExecutionOutcome>(RunErrors.AutomationNotConfigured);

        return await ExecuteScenarioRecordingAsync(run, runCase, executedByUserId, options, ct);
    }

    private async Task RefreshMissingAutomationSnapshotAsync(TestRun run, RunCase runCase, CancellationToken ct)
    {
        var current = await _db.TestCaseRefs.AsNoTracking()
            .Where(x => x.Id == runCase.TestCaseId && x.WorkspaceId == run.WorkspaceId)
            .Select(x => new
            {
                x.AutomationScenarioPath,
                x.AutomationEngine,
                x.AutomationStatus,
                x.AutomationDefinitionVersion,
                x.AutomationRecordingJson,
            })
            .FirstOrDefaultAsync(ct);

        if (current is null)
            return;

        var hasCurrentAutomation =
            !string.IsNullOrWhiteSpace(current.AutomationScenarioPath) ||
            (current.AutomationEngine == AutomationEngine.ScenarioRecording &&
             current.AutomationStatus == AutomationStatus.Published &&
             !string.IsNullOrWhiteSpace(current.AutomationRecordingJson));

        if (!hasCurrentAutomation)
            return;

        runCase.UpdateAutomationSnapshot(
            current.AutomationScenarioPath,
            current.AutomationEngine,
            current.AutomationStatus,
            current.AutomationDefinitionVersion,
            current.AutomationRecordingJson);
    }

    private async Task<Result<AutomaticRunCaseExecutionOutcome>> ExecuteScenarioRecordingAsync(
        TestRun run,
        RunCase runCase,
        Guid executedByUserId,
        AutomaticRunCaseExecutionOptions options,
        CancellationToken ct)
    {
        if (runCase.AutomationStatus != AutomationStatus.Published ||
            string.IsNullOrWhiteSpace(runCase.AutomationRecordingJson))
        {
            return Result.Failure<AutomaticRunCaseExecutionOutcome>(RunErrors.AutomationNotConfigured);
        }

        var definition = RecordedScenarioDefinition.FromJson(runCase.AutomationRecordingJson);
        var validation = RecordedScenarioValidator.Validate(definition);
        if (!validation.IsValid || validation.SanitizedDefinition is null)
        {
            var error = string.Join("; ", validation.Errors);
            return Result.Failure<AutomaticRunCaseExecutionOutcome>(
                Error.Validation("runs.automation_recording_invalid", error));
        }

        var sanitizedDefinition = validation.SanitizedDefinition;
        var prerequisites = await LoadScenarioPrerequisitesAsync(
            run.WorkspaceId, run.ProjectId, runCase.TestCaseId, ct);
        if (prerequisites.IsFailure)
            return Result.Failure<AutomaticRunCaseExecutionOutcome>(prerequisites.Error);

        var stopwatch = Stopwatch.StartNew();
        AutomationRunResponse response;
        try
        {
            response = await _automation.RunAsync(
                new AutomationRunRequest(
                    run.WorkspaceId,
                    run.ProjectId,
                    runCase.TestCaseId,
                    runCase.TestCaseCode,
                    sanitizedDefinition,
                    new AutomationRunConfiguration(
                        options.Gateway,
                        options.Frontend,
                        true,
                        sanitizedDefinition.TimeoutSeconds),
                    prerequisites.Value),
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (options.RecordFailures)
        {
            response = new AutomationRunResponse(
                false,
                AutomationFailureKind.RunnerFailure,
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                Array.Empty<AutomationStepResult>(),
                Array.Empty<AutomationNetworkResult>());
        }
        finally
        {
            stopwatch.Stop();
        }

        if (!response.Success &&
            !options.RecordFailures &&
            response.FailureKind == AutomationFailureKind.RunnerFailure)
        {
            return Result.Failure<AutomaticRunCaseExecutionOutcome>(
                Error.Failure("automation.runner_failure", response.ErrorMessage ?? "Automation runner failed."));
        }

        var status = response.Success ? ResultStatus.Passed : ResultStatus.Failed;
        var elapsedSeconds = Math.Max(1, (int)Math.Round(
            Math.Max(stopwatch.Elapsed.TotalSeconds, response.DurationMs / 1000d)));
        var scenarioName = string.IsNullOrWhiteSpace(sanitizedDefinition.Name)
            ? runCase.TestCaseCode
            : sanitizedDefinition.Name.Trim();
        var comment = BuildScenarioRecordingComment(
            options.CommentPrefix,
            scenarioName,
            status,
            response);
        var detailJson = JsonSerializer.Serialize(response, RecordedScenarioDefinition.JsonOptions);

        var recorded = runCase.RecordResult(
            status,
            comment,
            elapsedSeconds,
            executedByUserId,
            AutomationEngine.ScenarioRecording,
            detailJson);
        _db.RunResults.Add(recorded);
        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new TestResultRecorded(
            run.WorkspaceId, run.ProjectId, run.Id,
            runCase.Id, recorded.Id, runCase.TestCaseId, status.ToString(),
            executedByUserId, recorded.ExecutedAtUtc), ct);

        return new AutomaticRunCaseExecutionOutcome(
            recorded,
            scenarioName,
            response.Success,
            AutomationEngine.ScenarioRecording,
            response,
            response.ErrorMessage);
    }

    private async Task<Result<IReadOnlyList<RecordedScenarioDefinition>>> LoadScenarioPrerequisitesAsync(
        Guid workspaceId,
        Guid projectId,
        Guid testCaseId,
        CancellationToken ct)
    {
        var owner = await ReadScenarioLinkAsync(workspaceId, testCaseId, ct);
        if (owner?.PrerequisiteTestCaseId is not Guid)
            return Result.Success<IReadOnlyList<RecordedScenarioDefinition>>(Array.Empty<RecordedScenarioDefinition>());

        var links = new Dictionary<Guid, ScenarioPrerequisitePlanner.ScenarioLink>();
        var pending = new Queue<Guid>();
        pending.Enqueue(owner.PrerequisiteTestCaseId.Value);
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (links.ContainsKey(id))
                continue;

            var row = await ReadScenarioLinkAsync(workspaceId, id, ct);
            if (row is null)
            {
                links[id] = new ScenarioPrerequisitePlanner.ScenarioLink(id, projectId, string.Empty, null, null, true);
                continue;
            }

            links[id] = new ScenarioPrerequisitePlanner.ScenarioLink(
                row.Id,
                row.ProjectId,
                row.Title,
                row.PrerequisiteTestCaseId,
                row.RecordingJson,
                row.IsDeleted);
            if (row.PrerequisiteTestCaseId is Guid next && !links.ContainsKey(next))
                pending.Enqueue(next);
        }

        var plan = ScenarioPrerequisitePlanner.Create(
            testCaseId,
            projectId,
            owner.PrerequisiteTestCaseId,
            id => links.TryGetValue(id, out var link) ? link : null);
        if (!plan.Succeeded)
        {
            return Result.Failure<IReadOnlyList<RecordedScenarioDefinition>>(
                Error.Validation("runs.automation_prerequisite_invalid", plan.Error ?? "پیش‌نیاز اجرا معتبر نیست."));
        }

        return Result.Success<IReadOnlyList<RecordedScenarioDefinition>>(plan.Definitions);
    }

    private async Task<ScenarioLinkRow?> ReadScenarioLinkAsync(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var rows = await _db.Database.SqlQuery<ScenarioLinkRow>($"""
            SELECT "Id",
                   "ProjectId",
                   "Title",
                   "AutomationPrerequisiteTestCaseId" AS "PrerequisiteTestCaseId",
                   "AutomationRecordingJson" AS "RecordingJson",
                   ("DeletedAtUtc" IS NOT NULL) AS "IsDeleted"
            FROM testcases."testcases"
            WHERE "Id" = {id} AND "WorkspaceId" = {workspaceId}
            """).ToListAsync(ct);
        return rows.FirstOrDefault();
    }

    private sealed class ScenarioLinkRow
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid? PrerequisiteTestCaseId { get; set; }
        public string? RecordingJson { get; set; }
        public bool IsDeleted { get; set; }
    }

    private static string BuildScenarioRecordingComment(
        string prefix,
        string scenarioName,
        ResultStatus status,
        AutomationRunResponse response)
    {
        var lines = new List<string>
        {
            $"{prefix}: {scenarioName}",
            $"Engine: ScenarioRecording",
            $"Result: {status}",
            $"Duration: {response.DurationMs} ms",
            $"Actions/expectations: {response.Steps.Count}",
            $"Network responses: {response.Network.Count}",
        };

        if (!response.Success)
        {
            lines.Add($"Failure kind: {response.FailureKind}");
            if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
                lines.Add($"Error: {response.ErrorMessage}");
        }

        return string.Join(Environment.NewLine, lines);
    }

}
