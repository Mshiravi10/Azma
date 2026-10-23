namespace Azma.Automation;

public sealed class AutomationOptions
{
    public const string SectionName = "Automation";
    public const string HttpClientName = "Azma.Automation";

    public string BaseUrl { get; set; } = "http://localhost:5206";
    public int TimeoutSeconds { get; set; } = 300;

    public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Clamp(TimeoutSeconds, 1, 3600));
}
