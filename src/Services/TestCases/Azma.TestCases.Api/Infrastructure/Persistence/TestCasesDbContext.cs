using Azma.TestCases.Api.Domain;
using Azma.UserDirectory;
using Microsoft.EntityFrameworkCore;

namespace Azma.TestCases.Api.Infrastructure.Persistence;

public sealed class TestCasesDbContext : DbContext, IUserDirectoryDbContext
{
    public const string Schema = "testcases";

    public TestCasesDbContext(DbContextOptions<TestCasesDbContext> options) : base(options) { }

    public DbSet<ProjectRef> ProjectRefs => Set<ProjectRef>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<TestStep> TestSteps => Set<TestStep>();
    public DbSet<TestCaseTag> TestCaseTags => Set<TestCaseTag>();
    public DbSet<TestCaseVersion> TestCaseVersions => Set<TestCaseVersion>();
    public DbSet<AutomationRecordingSession> AutomationRecordingSessions => Set<AutomationRecordingSession>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<UserReadModel> UserDirectory => Set<UserReadModel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.ApplyConfigurationsFromAssembly(typeof(TestCasesDbContext).Assembly);
        b.ApplyConfiguration(new UserReadModelConfiguration());
    }
}
