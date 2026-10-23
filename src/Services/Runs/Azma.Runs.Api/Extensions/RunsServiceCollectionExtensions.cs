using Azma.Common.Application.Behaviors;
using Azma.Automation;
using Azma.Runs.Api.Application.Consumers;
using Azma.Runs.Api.Application.Runs;
using Azma.Runs.Api.Infrastructure.Persistence;
using Azma.UserDirectory;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Extensions;

public static class RunsServiceCollectionExtensions
{
    public static IServiceCollection AddRunsModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RunsDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npg => npg
                    .MigrationsHistoryTable("__ef_migrations_history", RunsDbContext.Schema)
                    .MigrationsAssembly(typeof(RunsDbContext).Assembly.FullName)));

        services.AddUserDirectory<RunsDbContext>();
        services.AddAzmaAutomationClient(configuration);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(RunsServiceCollectionExtensions).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(RunsServiceCollectionExtensions).Assembly);
        services.AddSingleton<RunScheduleSignal>();
        services.AddSingleton<RunAutomationSignal>();
        services.AddScoped<AutomaticRunCaseExecutor>();
        services.AddScoped<RunScheduleExecutor>();
        services.AddScoped<RunAutomationExecutor>();
        services.AddHostedService<RunScheduleWorker>();
        services.AddHostedService<RunAutomationWorker>();
        return services;
    }

    public static void RegisterRunsConsumers(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<ProjectCreatedConsumer>();
        cfg.AddConsumer<ProjectArchivedConsumer>();
        cfg.AddConsumer<ProjectRestoredConsumer>();
        cfg.AddConsumer<TestCaseCreatedConsumer>();
        cfg.AddConsumer<TestCaseUpdatedConsumer>();
        cfg.AddConsumer<TestCaseDeletedConsumer>();
        cfg.AddUserDirectoryConsumers("runs");
    }

    public static IHealthChecksBuilder AddRunsHealthChecks(
        this IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var pg = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(pg))
            builder.AddNpgSql(pg, name: "postgres");
        return builder;
    }
}
