using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Azma.Automation;

public sealed class AutomationApiClient : IAutomationApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AutomationOptions> _options;

    public AutomationApiClient(IHttpClientFactory httpClientFactory, IOptions<AutomationOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<AutomationValidationResponse> ValidateAsync(
        AutomationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "api/automation/validate",
            request,
            RecordedScenarioDefinition.JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new AutomationValidationResponse(false, [$"Automation API returned {(int)response.StatusCode}."]);

        return await response.Content.ReadFromJsonAsync<AutomationValidationResponse>(
                   RecordedScenarioDefinition.JsonOptions,
                   cancellationToken)
               ?? new AutomationValidationResponse(false, ["Automation API returned an empty response."]);
    }

    public async Task<AutomationRunResponse> RunAsync(
        AutomationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "api/automation/run",
            request,
            RecordedScenarioDefinition.JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new AutomationRunResponse(
                false,
                AutomationFailureKind.RunnerFailure,
                $"Automation API returned {(int)response.StatusCode}.",
                0,
                Array.Empty<AutomationStepResult>(),
                Array.Empty<AutomationNetworkResult>());
        }

        return await response.Content.ReadFromJsonAsync<AutomationRunResponse>(
                   RecordedScenarioDefinition.JsonOptions,
                   cancellationToken)
               ?? new AutomationRunResponse(
                   false,
                   AutomationFailureKind.RunnerFailure,
                   "Automation API returned an empty response.",
                   0,
                   Array.Empty<AutomationStepResult>(),
                   Array.Empty<AutomationNetworkResult>());
    }

    private HttpClient CreateClient()
    {
        var options = _options.Value;
        var client = _httpClientFactory.CreateClient(AutomationOptions.HttpClientName);
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = options.Timeout;
        return client;
    }
}
