using Azma.Reporting.Api.Domain;
using Azma.UserDirectory;
using Microsoft.EntityFrameworkCore;

namespace Azma.Reporting.Api.Infrastructure.Persistence;

public sealed class ReportingDbContext : DbContext, IUserDirectoryDbContext
{
    public const string Schema = "reporting";

    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options) { }

    public DbSet<ProjectRef> ProjectRefs => Set<ProjectRef>();
    public DbSet<TestCaseRef> TestCaseRefs => Set<TestCaseRef>();
    public DbSet<RunRef> Runs => Set<RunRef>();
    public DbSet<ResultEvent> ResultEvents => Set<ResultEvent>();
    public DbSet<UserReadModel> UserDirectory => Set<UserReadModel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
        b.ApplyConfiguration(new UserReadModelConfiguration());
    }
}
