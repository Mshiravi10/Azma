namespace Azma.Gateway.Mcp;

public sealed record AzmaMcpResult<T>(
    bool Ok,
    string Summary,
    T? Data,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> NextActions,
    string? SourceEndpoint)
{
    public static AzmaMcpResult<T> Success(
        string summary,
        T? data,
        string? sourceEndpoint,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? nextActions = null) =>
        new(true, summary, data, warnings ?? Array.Empty<string>(), nextActions ?? Array.Empty<string>(), sourceEndpoint);

    public static AzmaMcpResult<T> Failure(
        string summary,
        string? sourceEndpoint = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? nextActions = null) =>
        new(false, summary, default, warnings ?? Array.Empty<string>(), nextActions ?? Array.Empty<string>(), sourceEndpoint);
}
