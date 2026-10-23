using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Azma.EventBus.Extensions;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public delegate void ConfigureConsumers(IBusRegistrationConfigurator cfg);

public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Wires MassTransit + RabbitMQ. Pass a callback to register consumers
    /// (and <c>SetKebabCaseEndpointNameFormatter</c> is applied automatically).
    /// </summary>
    public static IServiceCollection AddAzmaEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var rabbit = configuration
            .GetSection(RabbitMqOptions.SectionName)
            .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddSingleton(rabbit);

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbit.Host, rabbit.Port, rabbit.VirtualHost, h =>
                {
                    h.Username(rabbit.Username);
                    h.Password(rabbit.Password);
                });

                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(15)));

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
