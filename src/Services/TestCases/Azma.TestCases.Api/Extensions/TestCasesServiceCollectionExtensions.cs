using Azma.Common.Application.Behaviors;
using Azma.Automation;
using Azma.TestCases.Api.Application.Consumers;
using Azma.TestCases.Api.Infrastructure.Persistence;
using Azma.UserDirectory;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.TestCases.Api.Extensions;

public static class TestCasesServiceCollectionExtensions
{
    public static IServiceCollection AddTestCasesModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TestCasesDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npg => npg
                    .MigrationsHistoryTable("__ef_migrations_history", TestCasesDbContext.Schema)
                    .MigrationsAssembly(typeof(TestCasesDbContext).Assembly.FullName)));

        services.AddUserDirectory<TestCasesDbContext>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(TestCasesServiceCollectionExtensions).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(TestCasesServiceCollectionExtensions).Assembly);
        services.AddAzmaAutomationClient(configuration);
        return services;
    }

    public static void RegisterTestCasesConsumers(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<ProjectCreatedConsumer>();
        cfg.AddConsumer<ProjectArchivedConsumer>();
        cfg.AddConsumer<ProjectRestoredConsumer>();
        cfg.AddConsumer<TestResultRecordedConsumer>();
        cfg.AddUserDirectoryConsumers("test-cases");
    }

    public static IHealthChecksBuilder AddTestCasesHealthChecks(
        this IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var pg = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(pg))
            builder.AddNpgSql(pg, name: "postgres");
        return builder;
    }
}
