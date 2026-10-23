using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Azma.Auth.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace Azma.Auth;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    AccessToken Generate(
        Guid userId,
        Guid workspaceId,
        string email,
        string displayName,
        string role,
        string accessScope = AzmaAccessScopes.Workspace);

    string GenerateRefreshToken();
}

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(JwtOptions options) => _options = options;

    public AccessToken Generate(
        Guid userId,
        Guid workspaceId,
        string email,
        string displayName,
        string role,
        string accessScope = AzmaAccessScopes.Workspace)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AzmaClaims.UserId, userId.ToString()),
            new(AzmaClaims.WorkspaceId, workspaceId.ToString()),
            new(AzmaClaims.Email, email),
            new(AzmaClaims.DisplayName, displayName),
            new(AzmaClaims.Role, role),
            new(AzmaClaims.AccessScope, string.IsNullOrWhiteSpace(accessScope) ? AzmaAccessScopes.Workspace : accessScope),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(jwt, expires);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
