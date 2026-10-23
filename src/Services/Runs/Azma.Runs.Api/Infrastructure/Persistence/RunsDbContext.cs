using Azma.Runs.Api.Domain;
using Azma.UserDirectory;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Infrastructure.Persistence;

public sealed class RunsDbContext : DbContext, IUserDirectoryDbContext
{
    public const string Schema = "runs";

    public RunsDbContext(DbContextOptions<RunsDbContext> options) : base(options) { }

    public DbSet<ProjectRef> ProjectRefs => Set<ProjectRef>();
    public DbSet<TestCaseRef> TestCaseRefs => Set<TestCaseRef>();
    public DbSet<TestRun> Runs => Set<TestRun>();
    public DbSet<RunCase> RunCases => Set<RunCase>();
    public DbSet<RunResult> RunResults => Set<RunResult>();
    public DbSet<RunComment> RunComments => Set<RunComment>();
    public DbSet<RunAutomationExecution> RunAutomationExecutions => Set<RunAutomationExecution>();
    public DbSet<RunAttachment> RunAttachments => Set<RunAttachment>();
    public DbSet<RunSchedule> RunSchedules => Set<RunSchedule>();
    public DbSet<RunScheduleCase> RunScheduleCases => Set<RunScheduleCase>();
    public DbSet<RunScheduleExecution> RunScheduleExecutions => Set<RunScheduleExecution>();
    public DbSet<UserReadModel> UserDirectory => Set<UserReadModel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.ApplyConfigurationsFromAssembly(typeof(RunsDbContext).Assembly);
        b.ApplyConfiguration(new UserReadModelConfiguration());
    }
}
