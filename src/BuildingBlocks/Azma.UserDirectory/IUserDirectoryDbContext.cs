namespace Azma.UserDirectory;

/// <summary>
/// Implemented by a service's DbContext to expose the shared user directory table
/// to the user-directory consumers without introducing a circular dependency.
/// </summary>
public interface IUserDirectoryDbContext
{
    DbSet<UserReadModel> UserDirectory { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
