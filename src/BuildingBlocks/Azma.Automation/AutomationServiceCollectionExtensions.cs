using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Azma.Automation;

public static class AutomationServiceCollectionExtensions
{
    public static IServiceCollection AddAzmaAutomationClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AutomationOptions>()
            .Bind(configuration.GetSection(AutomationOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.BaseUrl), "Automation BaseUrl is required.")
            .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _), "Automation BaseUrl must be an absolute URL.")
            .Validate(x => x.TimeoutSeconds > 0, "Automation TimeoutSeconds must be greater than zero.");

        services.AddHttpClient(AutomationOptions.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AutomationOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        services.AddScoped<IAutomationApiClient, AutomationApiClient>();
        return services;
    }
}
