using Azma.EventBus.Contracts.Projects;
using Azma.EventBus.Contracts.Runs;
using Azma.EventBus.Contracts.TestCases;
using Azma.Reporting.Api.Domain;
using Azma.Reporting.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Azma.Reporting.Api.Application.Consumers;

public sealed class ProjectCreatedConsumer : IConsumer<ProjectCreated>
{
    private readonly ReportingDbContext _db;
    private readonly ILogger<ProjectCreatedConsumer> _logger;
    public ProjectCreatedConsumer(ReportingDbContext db, ILogger<ProjectCreatedConsumer> logger)
    { _db = db; _logger = logger; }

    public async Task Consume(ConsumeContext<ProjectCreated> ctx)
    {
        var msg = ctx.Message;
        var existing = await _db.ProjectRefs.FindAsync(new object[] { msg.ProjectId }, ctx.CancellationToken);
        if (existing is not null) return;
        _db.ProjectRefs.Add(ProjectRef.Create(msg.ProjectId, msg.WorkspaceId, msg.Code, msg.Name));
        await _db.SaveChangesAsync(ctx.CancellationToken);
        _logger.LogInformation("Reporting: registered project {ProjectId} ({Code}).", msg.ProjectId, msg.Code);
    }
}

public sealed class ProjectArchivedConsumer : IConsumer<ProjectArchived>
{
    private readonly ReportingDbContext _db;
    public ProjectArchivedConsumer(ReportingDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<ProjectArchived> ctx)
    {
        var p = await _db.ProjectRefs.FirstOrDefaultAsync(
            x => x.Id == ctx.Message.ProjectId && x.WorkspaceId == ctx.Message.WorkspaceId, ctx.CancellationToken);
        if (p is null || p.IsArchived) return;
        p.Archive();
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}

public sealed class TestCaseCreatedConsumer : IConsumer<TestCaseCreated>
{
    private readonly ReportingDbContext _db;
    public TestCaseCreatedConsumer(ReportingDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<TestCaseCreated> ctx)
    {
        var msg = ctx.Message;
        var existing = await _db.TestCaseRefs.FindAsync(new object[] { msg.TestCaseId }, ctx.CancellationToken);
        if (existing is not null) { existing.Apply(msg.Code, msg.Title); }
        else
        {
            _db.TestCaseRefs.Add(TestCaseRef.Create(msg.TestCaseId, msg.WorkspaceId, msg.ProjectId, msg.Code, msg.Title));
        }
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}

public sealed class TestCaseUpdatedConsumer : IConsumer<TestCaseUpdated>
{
    private readonly ReportingDbContext _db;
    public TestCaseUpdatedConsumer(ReportingDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<TestCaseUpdated> ctx)
    {
        var msg = ctx.Message;
        var t = await _db.TestCaseRefs.FirstOrDefaultAsync(x => x.Id == msg.TestCaseId, ctx.CancellationToken);
        if (t is null) _db.TestCaseRefs.Add(TestCaseRef.Create(msg.TestCaseId, msg.WorkspaceId, msg.ProjectId, msg.Code, msg.Title));
        else t.Apply(msg.Code, msg.Title);
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}

public sealed class TestCaseDeletedConsumer : IConsumer<TestCaseDeleted>
{
    private readonly ReportingDbContext _db;
    public TestCaseDeletedConsumer(ReportingDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<TestCaseDeleted> ctx)
    {
        var t = await _db.TestCaseRefs.FirstOrDefaultAsync(x => x.Id == ctx.Message.TestCaseId, ctx.CancellationToken);
        if (t is null || t.IsDeleted) return;
        t.MarkDeleted();
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}

public sealed class TestRunCreatedConsumer : IConsumer<TestRunCreated>
{
    private readonly ReportingDbContext _db;
    private readonly ILogger<TestRunCreatedConsumer> _logger;
    public TestRunCreatedConsumer(ReportingDbContext db, ILogger<TestRunCreatedConsumer> logger)
    { _db = db; _logger = logger; }

    public async Task Consume(ConsumeContext<TestRunCreated> ctx)
    {
        var msg = ctx.Message;
        var existing = await _db.Runs.FindAsync(new object[] { msg.RunId }, ctx.CancellationToken);
        if (existing is not null) return;
        _db.Runs.Add(RunRef.Create(msg.RunId, msg.WorkspaceId, msg.ProjectId, msg.Name, msg.CaseCount));
        await _db.SaveChangesAsync(ctx.CancellationToken);
        _logger.LogInformation("Reporting: registered run {RunId} ({Name}) cases={CaseCount}.",
            msg.RunId, msg.Name, msg.CaseCount);
    }
}

public sealed class TestRunCompletedConsumer : IConsumer<TestRunCompleted>
{
    private readonly ReportingDbContext _db;
    public TestRunCompletedConsumer(ReportingDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<TestRunCompleted> ctx)
    {
        var msg = ctx.Message;
        var run = await _db.Runs.FirstOrDefaultAsync(x => x.Id == msg.RunId, ctx.CancellationToken);
        if (run is null)
        {
            // Out-of-order safety: synthesize minimal projection.
            run = RunRef.Create(msg.RunId, msg.WorkspaceId, msg.ProjectId, "(unknown)",
                msg.Passed + msg.Failed + msg.Blocked + msg.Skipped);
            _db.Runs.Add(run);
        }
        run.Complete(msg.Passed, msg.Failed, msg.Blocked, msg.Skipped);
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}

public sealed class TestResultRecordedConsumer : IConsumer<TestResultRecorded>
{
    private readonly ReportingDbContext _db;
    public TestResultRecordedConsumer(ReportingDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<TestResultRecorded> ctx)
    {
        var msg = ctx.Message;
        if (!Enum.TryParse<ReportResultStatus>(msg.Status, ignoreCase: true, out var status))
            return;

        // Idempotency: same RunCase + same ExecutedAt within ms — skip duplicates.
        // Simpler approach: rely on bus-level retries being rare; persist event row.
        var ev = ResultEvent.Create(
            msg.WorkspaceId, msg.ProjectId, msg.RunId, msg.RunCaseId, msg.TestCaseId,
            status, msg.ExecutedByUserId, msg.ExecutedAtUtc);
        _db.ResultEvents.Add(ev);

        // Update aggregate counters on the run projection (the closing event will overwrite definitively).
        var run = await _db.Runs.FirstOrDefaultAsync(x => x.Id == msg.RunId, ctx.CancellationToken);
        if (run is null)
        {
            run = RunRef.Create(msg.RunId, msg.WorkspaceId, msg.ProjectId, "(unknown)", 0);
            _db.Runs.Add(run);
        }
        run.RecordResult(status);
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}
