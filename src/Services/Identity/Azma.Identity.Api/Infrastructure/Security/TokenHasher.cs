using System.Security.Cryptography;
using System.Text;

namespace Azma.Identity.Api.Infrastructure.Security;

public interface ITokenHasher
{
    /// <summary>Generates a URL-safe random token and returns (raw, sha256-hash).</summary>
    (string Raw, string Hash) GenerateAndHash(int byteLength = 48);

    string Hash(string raw);
}

public sealed class Sha256TokenHasher : ITokenHasher
{
    public (string Raw, string Hash) GenerateAndHash(int byteLength = 48)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        var raw = Base64Url(bytes);
        return (raw, Hash(raw));
    }

    public string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
