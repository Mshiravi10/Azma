using System.Reflection;
using Azma.Automation;
using Azma.EventBus.Contracts.TestCases;
using Azma.Runs.Api.Application.Consumers;
using Azma.Runs.Api.Application.Runs;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Azma.Runs.Tests;

public sealed class SchedulePublicationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Publish_updates_active_and_paused_schedules_but_preserves_existing_runs_and_other_scopes()
    {
        await using var db = fixture.CreateContext();
        var publication = Publication();
        var active = Schedule(publication);
        var paused = Schedule(publication);
        paused.Pause();
        var deleted = Schedule(publication);
        deleted.SoftDelete();
        var otherWorkspace = Schedule(publication with { WorkspaceId = Guid.NewGuid() });
        var otherProject = Schedule(publication with { ProjectId = Guid.NewGuid() });
        db.RunSchedules.AddRange(active, paused, deleted, otherWorkspace, otherProject);

        var run = TestRun.Create(publication.WorkspaceId, publication.ProjectId, null,
            "TEST-R-001", 1, "Historical run", null, Guid.NewGuid());
        var runCase = RunCase.Snapshot(run.Id, publication.TestCaseId, publication.Code,
            "Old login", 4, null, null, AutomationEngine.ScenarioRecording,
            AutomationStatus.Published, 1, Recording("***"));
        runCase.RecordResult(ResultStatus.Failed, "Original failure", 20, Guid.NewGuid());
        run.Cases.Add(runCase);
        db.Runs.Add(run);
        await db.SaveChangesAsync();

        var consumer = new TestCaseUpdatedConsumer(db, NullLogger<TestCaseUpdatedConsumer>.Instance);
        await consumer.Consume(MessageProxy.Context(publication));
        db.ChangeTracker.Clear();

        foreach (var schedule in new[] { active, paused })
        {
            var updated = await db.RunScheduleCases.SingleAsync(c => c.RunScheduleId == schedule.Id);
            Assert.Equal(6, updated.TestCaseVersion);
            Assert.Equal(3, updated.AutomationDefinitionVersion);
            Assert.Equal("new-password", Password(updated.AutomationRecordingJson));
        }
        foreach (var schedule in new[] { deleted, otherWorkspace, otherProject })
        {
            var unchanged = await db.RunScheduleCases.SingleAsync(c => c.RunScheduleId == schedule.Id);
            Assert.Equal("***", Password(unchanged.AutomationRecordingJson));
        }
        var historical = await db.RunCases.Include(c => c.Results).SingleAsync(c => c.Id == runCase.Id);
        Assert.Equal("***", Password(historical.AutomationRecordingJson));
        Assert.Equal("Original failure", Assert.Single(historical.Results).Comment);
    }

    [Fact]
    public async Task Drafts_validation_and_delayed_publications_do_not_replace_latest_published_recording()
    {
        await using var db = fixture.CreateContext();
        var publication = Publication();
        var schedule = Schedule(publication);
        db.RunSchedules.Add(schedule);
        await db.SaveChangesAsync();

        await RunScheduleAutomationSynchronizer.ApplyPublicationAsync(db, publication, default);
        foreach (var status in new[] { "Draft", "Validated", "Broken" })
        {
            var affected = await RunScheduleAutomationSynchronizer.ApplyPublicationAsync(db,
                publication with { Version = 7, AutomationDefinitionVersion = 4,
                    AutomationStatus = status, AutomationRecordingJson = Recording("unpublished") }, default);
            Assert.Equal(0, affected);
        }
        Assert.Equal(0, await RunScheduleAutomationSynchronizer.ApplyPublicationAsync(db,
            publication with { Version = 5, AutomationDefinitionVersion = 2,
                AutomationRecordingJson = Recording("old-password") }, default));
        // Duplicate deliveries remain safe.
        await RunScheduleAutomationSynchronizer.ApplyPublicationAsync(db, publication, default);
        db.ChangeTracker.Clear();
        var saved = await db.RunScheduleCases.SingleAsync(c => c.RunScheduleId == schedule.Id);
        Assert.Equal("new-password", Password(saved.AutomationRecordingJson));
        Assert.Equal(3, saved.AutomationDefinitionVersion);
    }

    [Fact]
    public async Task Scheduled_execution_repairs_old_snapshot_and_uses_it_even_when_cases_were_already_tracked()
    {
        await using var db = fixture.CreateContext();
        var publication = Publication();
        var schedule = Schedule(publication);
        db.RunSchedules.Add(schedule);
        db.ProjectRefs.Add(ProjectRef.Create(publication.ProjectId, publication.WorkspaceId, "TEST"));
        await db.SaveChangesAsync();
        await InsertSourceAsync(db, publication);

        var automation = new CapturingAutomationClient();
        var bus = DispatchProxy.Create<IPublishEndpoint, MessageProxy>();
        var caseExecutor = new AutomaticRunCaseExecutor(db, automation, bus);
        var executor = new RunScheduleExecutor(db, caseExecutor, bus,
            NullLogger<RunScheduleExecutor>.Instance);
        await executor.ExecuteAsync(schedule.Id, "test", default);

        var request = Assert.Single(automation.Requests);
        Assert.Equal("new-password", Assert.Single(request.Definition.Actions).Value);
        db.ChangeTracker.Clear();
        var execution = await db.RunScheduleExecutions.SingleAsync(e => e.RunScheduleId == schedule.Id);
        Assert.Equal(1, execution.Passed);
        var runCase = await db.RunCases.SingleAsync(c => c.RunId == execution.RunId);
        Assert.Equal(6, runCase.TestCaseVersion);
        Assert.Equal(3, runCase.AutomationDefinitionVersion);
        Assert.Equal("new-password", Password(runCase.AutomationRecordingJson));
    }

    [Theory]
    [InlineData("Draft", false, false)]
    [InlineData("Validated", false, false)]
    [InlineData("Published", true, false)]
    [InlineData("Published", false, true)]
    public async Task Pre_run_refresh_keeps_published_snapshot_for_draft_deleted_or_foreign_sources(
        string status, bool deleted, bool foreignWorkspace)
    {
        await using var db = fixture.CreateContext();
        var publication = Publication();
        var schedule = Schedule(publication);
        db.RunSchedules.Add(schedule);
        await db.SaveChangesAsync();
        await InsertSourceAsync(db, publication with {
            AutomationStatus = status,
            WorkspaceId = foreignWorkspace ? Guid.NewGuid() : publication.WorkspaceId }, deleted);

        Assert.Equal(0, await RunScheduleAutomationSynchronizer.RefreshBeforeRunAsync(db, schedule, default));
        db.ChangeTracker.Clear();
        var saved = await db.RunScheduleCases.SingleAsync(c => c.RunScheduleId == schedule.Id);
        Assert.Equal("***", Password(saved.AutomationRecordingJson));
    }

    private static TestCaseUpdated Publication() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TEST-T-001", "Login", 6,
        AutomationEngine: "ScenarioRecording", AutomationStatus: "Published",
        AutomationDefinitionVersion: 3, AutomationRecordingJson: Recording("new-password"));

    private static RunSchedule Schedule(TestCaseUpdated source)
    {
        var schedule = RunSchedule.Create(source.WorkspaceId, source.ProjectId, "Login schedule",
            null, null, ScheduleRecurrenceType.Daily, 540, 0, "UTC", null, null,
            Guid.NewGuid(), 1, 1, 0, false, null, null);
        schedule.Cases.Add(RunScheduleCase.Snapshot(schedule.Id, source.TestCaseId, source.Code,
            "Old login", 4, null, AutomationEngine.ScenarioRecording, AutomationStatus.Published,
            1, Recording("***")));
        return schedule;
    }

    private static string Recording(string password) => new RecordedScenarioDefinition(
        "Login", "https://example.invalid/", [new(RecordedActionType.Fill,
            Selector: "input[name='password']", Value: password, Sensitive: true)], [], []).ToJson();

    private static string? Password(string? recording) =>
        RecordedScenarioDefinition.FromJson(recording)!.Actions.Single().Value;

    private static Task<int> InsertSourceAsync(RunsDbContext db, TestCaseUpdated source, bool deleted = false)
    {
        DateTime? deletedAt = deleted ? DateTime.UtcNow : null;
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO testcases.testcases
                ("Id", "WorkspaceId", "ProjectId", "Code", "Title", "Version", "AutomationEngine",
                 "AutomationStatus", "AutomationDefinitionVersion", "AutomationRecordingJson", "DeletedAtUtc")
            VALUES ({source.TestCaseId}, {source.WorkspaceId}, {source.ProjectId}, {source.Code},
                {source.Title}, {source.Version}, {(int)AutomationEngine.ScenarioRecording},
                {(int)Enum.Parse<AutomationStatus>(source.AutomationStatus!)},
                {source.AutomationDefinitionVersion}, CAST({source.AutomationRecordingJson} AS jsonb), {deletedAt})
            """);
    }

    private sealed class CapturingAutomationClient : IAutomationApiClient
    {
        public List<AutomationRunRequest> Requests { get; } = [];
        public Task<AutomationRunResponse> RunAsync(AutomationRunRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new AutomationRunResponse(true, AutomationFailureKind.None, null, 1, [], []));
        }
        public Task<AutomationValidationResponse> ValidateAsync(AutomationValidationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(RecordedScenarioValidator.Validate(request.Definition));
    }
}

public class MessageProxy : DispatchProxy
{
    public TestCaseUpdated? Message { get; set; }
    public static ConsumeContext<TestCaseUpdated> Context(TestCaseUpdated message)
    {
        var context = Create<ConsumeContext<TestCaseUpdated>, MessageProxy>();
        ((MessageProxy)(object)context).Message = message;
        return context;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    {
        "get_Message" => Message,
        "get_CancellationToken" => CancellationToken.None,
        "Publish" => Task.CompletedTask,
        _ => throw new NotSupportedException(method?.Name),
    };
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();
    public RunsDbContext CreateContext() => new(new DbContextOptionsBuilder<RunsDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA testcases;
            CREATE TABLE testcases.testcases (
                "Id" uuid PRIMARY KEY, "WorkspaceId" uuid NOT NULL, "ProjectId" uuid NOT NULL,
                "Code" text NOT NULL, "Title" text NOT NULL, "Version" integer NOT NULL,
                "AutomationScenarioPath" text, "AutomationEngine" integer NOT NULL,
                "AutomationStatus" integer NOT NULL, "AutomationDefinitionVersion" integer,
                "AutomationRecordingJson" jsonb, "AutomationPrerequisiteTestCaseId" uuid,
                "DeletedAtUtc" timestamp with time zone);
            """);
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
