namespace Azma.Automation;

public static class OAuthReplay
{
    public static IReadOnlyList<RecordedScenarioAction> Prepare(IReadOnlyList<RecordedScenarioAction>? actions)
    {
        if (actions is null || actions.Count == 0)
            return Array.Empty<RecordedScenarioAction>();

        var prepared = new List<RecordedScenarioAction>(actions.Count);
        for (var index = 0; index < actions.Count;)
        {
            if (actions[index].Type is not (RecordedActionType.Click or RecordedActionType.Submit))
            {
                prepared.Add(actions[index]);
                index++;
                continue;
            }

            var end = index;
            while (end < actions.Count && IsFormStep(actions[end].Type))
                end++;

            var fillAfterTrigger = false;
            var seenTrigger = false;
            for (var cursor = index; cursor < end; cursor++)
            {
                if (actions[cursor].Type is RecordedActionType.Click or RecordedActionType.Submit)
                    seenTrigger = true;
                else if (seenTrigger && actions[cursor].Type is RecordedActionType.Fill or RecordedActionType.Select)
                    fillAfterTrigger = true;
            }

            if (!fillAfterTrigger)
            {
                for (var cursor = index; cursor < end; cursor++)
                    prepared.Add(actions[cursor]);
            }
            else
            {
                for (var cursor = index; cursor < end; cursor++)
                {
                    if (actions[cursor].Type is RecordedActionType.Fill or RecordedActionType.Select)
                        prepared.Add(actions[cursor]);
                }

                for (var cursor = index; cursor < end; cursor++)
                {
                    if (actions[cursor].Type is not (RecordedActionType.Fill or RecordedActionType.Select))
                        prepared.Add(actions[cursor]);
                }
            }

            index = end;
        }

        return prepared;
    }

    public static bool IsAuthorizationRequest(string? url)
    {
        var query = Query(url);
        return Has(query, "response_type") &&
               Has(query, "client_id") &&
               Has(query, "redirect_uri") &&
               Has(query, "state");
    }

    public static bool IsCallback(string? url)
    {
        var query = Query(url);
        if (!Has(query, "code"))
            return false;

        if (Has(query, "state") || Has(query, "responseType") || Has(query, "response_type"))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               uri.AbsolutePath.Contains("/sso/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLoginUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Host.Contains("authenticator", StringComparison.OrdinalIgnoreCase))
            return true;

        return uri.AbsolutePath.Contains("/login", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFormStep(RecordedActionType type) =>
        type is RecordedActionType.Click or
            RecordedActionType.Submit or
            RecordedActionType.Fill or
            RecordedActionType.Select or
            RecordedActionType.Wait;

    private static bool Has(Dictionary<string, string> query, string key) =>
        query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    private static Dictionary<string, string> Query(string? url)
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
            return query;

        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? part : part[..separator]);
            var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(part[(separator + 1)..]);
            if (!string.IsNullOrWhiteSpace(key))
                query[key] = value;
        }

        return query;
    }
}
