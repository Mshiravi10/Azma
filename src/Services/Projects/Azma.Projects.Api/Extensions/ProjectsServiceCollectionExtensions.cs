using Azma.Common.Application.Behaviors;
using Azma.Projects.Api.Application.Consumers;
using Azma.Projects.Api.Infrastructure.Persistence;
using Azma.UserDirectory;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Projects.Api.Extensions;

public static class ProjectsServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProjectsDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npg => npg
                    .MigrationsHistoryTable("__ef_migrations_history", ProjectsDbContext.Schema)
                    .MigrationsAssembly(typeof(ProjectsDbContext).Assembly.FullName)));

        services.AddUserDirectory<ProjectsDbContext>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ProjectsServiceCollectionExtensions).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(ProjectsServiceCollectionExtensions).Assembly);

        return services;
    }

    public static void RegisterProjectsConsumers(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<WorkspaceCreatedConsumer>();
        cfg.AddConsumer<MemberRemovedConsumer>();
        cfg.AddUserDirectoryConsumers("projects");
    }

    public static IHealthChecksBuilder AddProjectsHealthChecks(
        this IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var pg = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(pg))
            builder.AddNpgSql(pg, name: "postgres");
        return builder;
    }
}
