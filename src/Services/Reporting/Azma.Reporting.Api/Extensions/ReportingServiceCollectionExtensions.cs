using Azma.Common.Application.Behaviors;
using Azma.Reporting.Api.Application.Consumers;
using Azma.Reporting.Api.Infrastructure.Persistence;
using Azma.UserDirectory;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Reporting.Api.Extensions;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReportingModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ReportingDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npg => npg
                    .MigrationsHistoryTable("__ef_migrations_history", ReportingDbContext.Schema)
                    .MigrationsAssembly(typeof(ReportingDbContext).Assembly.FullName)));

        services.AddUserDirectory<ReportingDbContext>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ReportingServiceCollectionExtensions).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(ReportingServiceCollectionExtensions).Assembly);
        return services;
    }

    public static void RegisterReportingConsumers(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<ProjectCreatedConsumer>();
        cfg.AddConsumer<ProjectArchivedConsumer>();
        cfg.AddConsumer<TestCaseCreatedConsumer>();
        cfg.AddConsumer<TestCaseUpdatedConsumer>();
        cfg.AddConsumer<TestCaseDeletedConsumer>();
        cfg.AddConsumer<TestRunCreatedConsumer>();
        cfg.AddConsumer<TestRunCompletedConsumer>();
        cfg.AddConsumer<TestResultRecordedConsumer>();
        cfg.AddUserDirectoryConsumers("reporting");
    }

    public static IHealthChecksBuilder AddReportingHealthChecks(
        this IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var pg = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(pg))
            builder.AddNpgSql(pg, name: "postgres");
        return builder;
    }
}
