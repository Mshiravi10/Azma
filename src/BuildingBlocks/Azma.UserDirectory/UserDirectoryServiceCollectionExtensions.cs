using Azma.UserDirectory.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Azma.UserDirectory;

public static class UserDirectoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the bridge that lets the UserDirectory consumers find the host
    /// service's <typeparamref name="TDbContext"/> through <see cref="IUserDirectoryDbContext"/>.
    /// Call once during service bootstrap, after the DbContext is registered.
    /// </summary>
    public static IServiceCollection AddUserDirectory<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext, IUserDirectoryDbContext
    {
        services.AddScoped<IUserDirectoryDbContext>(sp => sp.GetRequiredService<TDbContext>());
        return services;
    }

    /// <summary>
    /// Registers UserDirectory consumers on the MassTransit bus. Call from inside
    /// the <c>configureConsumers</c> callback passed to <c>AddAzmaEventBus</c>.
    /// </summary>
    /// <param name="cfg">The bus registration configurator.</param>
    /// <param name="serviceName">
    /// Per-service prefix (e.g. "projects", "test-cases", "runs", "reporting"). Used to
    /// give each service its own RabbitMQ queue so every service receives a copy of the
    /// broadcast user-* events instead of competing for a shared queue.
    /// </param>
    public static void AddUserDirectoryConsumers(this IBusRegistrationConfigurator cfg, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentException("Service name is required.", nameof(serviceName));

        cfg.AddConsumer<UserUpdatedConsumer>().Endpoint(e => e.Name = $"{serviceName}-user-updated");
        cfg.AddConsumer<UserAvatarChangedConsumer>().Endpoint(e => e.Name = $"{serviceName}-user-avatar-changed");
        cfg.AddConsumer<UserDeactivatedConsumer>().Endpoint(e => e.Name = $"{serviceName}-user-deactivated");
    }
}
