using Azma.EventBus.Contracts.Runs;
using Azma.TestCases.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Azma.TestCases.Api.Application.Consumers;

public sealed class TestResultRecordedConsumer : IConsumer<TestResultRecorded>
{
    private readonly TestCasesDbContext _db;

    public TestResultRecordedConsumer(TestCasesDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<TestResultRecorded> ctx)
    {
        var msg = ctx.Message;
        var testCase = await _db.TestCases.FirstOrDefaultAsync(
            x => x.Id == msg.TestCaseId && x.WorkspaceId == msg.WorkspaceId,
            ctx.CancellationToken);

        if (testCase is null)
            return;

        var changed = testCase.RecordExecution(
            msg.Status,
            msg.RunId,
            msg.RunCaseId,
            msg.ExecutedByUserId,
            msg.ExecutedAtUtc);

        if (changed)
            await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}
