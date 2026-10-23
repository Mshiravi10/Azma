using System.Net;
using System.Text;
using Azma.Gateway.Mcp;
using Microsoft.Extensions.Options;

namespace Azma.Gateway.Tests;

internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public void EnqueueJson(HttpStatusCode statusCode, string json)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    public void Enqueue(HttpStatusCode statusCode)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode));
    }

    public void EnqueueException(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(CloneForInspection(request));

        if (_responses.Count == 0)
            throw new InvalidOperationException("No fake HTTP response was queued.");

        return Task.FromResult(_responses.Dequeue()(request));
    }

    private static HttpRequestMessage CloneForInspection(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public StubHttpClientFactory(HttpMessageHandler handler) =>
        _client = new HttpClient(handler);

    public HttpClient CreateClient(string name) => _client;
}

internal sealed class StubOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StubOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; private set; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal sealed class StubTokenProvider : IAzmaTokenProvider
{
    private readonly string? _token;

    public StubTokenProvider(string? token) => _token = token;

    public int Invalidations { get; private set; }

    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_token);

    public void Invalidate() => Invalidations++;
}
