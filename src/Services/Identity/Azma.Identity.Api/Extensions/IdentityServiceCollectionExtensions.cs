using Azma.Common.Application.Behaviors;
using Azma.Identity.Api.Application.Members;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Extensions;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npg => npg
                    .MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.Schema)
                    .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));

        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(IdentityServiceCollectionExtensions).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(IdentityServiceCollectionExtensions).Assembly);

        return services;
    }

    public static IHealthChecksBuilder AddIdentityHealthChecks(
        this IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var pg = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(pg))
            builder.AddNpgSql(pg, name: "postgres");
        return builder;
    }

    public static void RegisterIdentityConsumers(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<EnsureWorkspaceMemberConsumer>();
    }
}
