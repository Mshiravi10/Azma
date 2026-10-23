using Azma.Projects.Api.Domain;
using Azma.UserDirectory;
using Microsoft.EntityFrameworkCore;

namespace Azma.Projects.Api.Infrastructure.Persistence;

public sealed class ProjectsDbContext : DbContext, IUserDirectoryDbContext
{
    public const string Schema = "projects";

    public ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Suite> Suites => Set<Suite>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<ProjectMembership> ProjectMemberships => Set<ProjectMembership>();
    public DbSet<UserReadModel> UserDirectory => Set<UserReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProjectsDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new UserReadModelConfiguration());
    }
}
