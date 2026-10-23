namespace Azma.Automation;

public interface IAutomationApiClient
{
    Task<AutomationValidationResponse> ValidateAsync(
        AutomationValidationRequest request,
        CancellationToken cancellationToken = default);

    Task<AutomationRunResponse> RunAsync(
        AutomationRunRequest request,
        CancellationToken cancellationToken = default);
}
