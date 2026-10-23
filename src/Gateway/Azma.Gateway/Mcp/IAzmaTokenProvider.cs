namespace Azma.Gateway.Mcp;

public interface IAzmaTokenProvider
{
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken);

    void Invalidate();
}
