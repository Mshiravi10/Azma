using Azma.Automation;
using Azma.EventBus.Contracts.TestCases;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Application.Runs;

internal static class RunScheduleAutomationSynchronizer
{
    public static Task<int> ApplyPublicationAsync(
        RunsDbContext db, TestCaseUpdated publication, CancellationToken ct)
    {
        if (!Enum.TryParse<AutomationEngine>(publication.AutomationEngine, true, out var engine) ||
            engine != AutomationEngine.ScenarioRecording ||
            !Enum.TryParse<AutomationStatus>(publication.AutomationStatus, true, out var status) ||
            status != AutomationStatus.Published ||
            string.IsNullOrWhiteSpace(publication.AutomationRecordingJson))
        {
            return Task.FromResult(0);
        }

        // Draft/validated events never replace the last published schedule definition.
        // Version checks also prevent a delayed publication from rolling it back.
        var definitionVersion = publication.AutomationDefinitionVersion ?? 0;
        return db.RunScheduleCases
            .Where(c => c.TestCaseId == publication.TestCaseId &&
                        c.TestCaseVersion <= publication.Version &&
                        (c.AutomationDefinitionVersion ?? 0) <= definitionVersion &&
                        db.RunSchedules.Any(s => s.Id == c.RunScheduleId &&
                            s.WorkspaceId == publication.WorkspaceId &&
                            s.ProjectId == publication.ProjectId &&
                            s.DeletedAtUtc == null))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.TestCaseCode, publication.Code)
                .SetProperty(c => c.TestCaseTitle, publication.Title)
                .SetProperty(c => c.TestCaseVersion, publication.Version)
                .SetProperty(c => c.AutomationScenarioPath, publication.AutomationScenarioPath)
                .SetProperty(c => c.AutomationEngine, engine)
                .SetProperty(c => c.AutomationStatus, status)
                .SetProperty(c => c.AutomationDefinitionVersion, publication.AutomationDefinitionVersion)
                .SetProperty(c => c.AutomationRecordingJson, publication.AutomationRecordingJson), ct);
    }

    public static Task<int> RefreshBeforeRunAsync(
        RunsDbContext db, RunSchedule schedule, CancellationToken ct)
    {
        // Read the source before taking the run snapshot: this also repairs schedules
        // created before publication syncing existed, or when event delivery is delayed.
        // When the source has an unpublished draft, keep the last published snapshot.
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE runs."run_schedule_cases" AS c
            SET "TestCaseCode" = t."Code",
                "TestCaseTitle" = t."Title",
                "TestCaseVersion" = t."Version",
                "AutomationScenarioPath" = t."AutomationScenarioPath",
                "AutomationEngine" = t."AutomationEngine",
                "AutomationStatus" = t."AutomationStatus",
                "AutomationDefinitionVersion" = t."AutomationDefinitionVersion",
                "AutomationRecordingJson" = t."AutomationRecordingJson"
            FROM testcases."testcases" AS t
            WHERE c."RunScheduleId" = {schedule.Id}
              AND c."TestCaseId" = t."Id"
              AND t."WorkspaceId" = {schedule.WorkspaceId}
              AND t."ProjectId" = {schedule.ProjectId}
              AND t."DeletedAtUtc" IS NULL
              AND t."AutomationEngine" = {(int)AutomationEngine.ScenarioRecording}
              AND t."AutomationStatus" = {(int)AutomationStatus.Published}
              AND t."AutomationRecordingJson" IS NOT NULL
              AND t."AutomationRecordingJson" <> 'null'::jsonb
              AND c."TestCaseVersion" <= t."Version"
              AND COALESCE(c."AutomationDefinitionVersion", 0) <= COALESCE(t."AutomationDefinitionVersion", 0)
            """, ct);
    }
}
