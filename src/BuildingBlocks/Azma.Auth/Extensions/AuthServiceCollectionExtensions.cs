using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Azma.Auth.Jwt;
using Azma.Auth.Tenant;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Azma.Auth.Extensions;

public static class AuthServiceCollectionExtensions
{
    /// <summary>Adds JWT bearer auth, tenant context and JWT token generator.</summary>
    public static IServiceCollection AddAzmaAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Don't remap "sub" -> ClaimTypes.NameIdentifier etc.
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        var section = configuration.GetSection(JwtOptions.SectionName);
        var options = section.Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(options.SigningKey))
            throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        services.AddSingleton(options);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AzmaClaims.Email,
                    RoleClaimType = AzmaClaims.Role,
                };
            });

        services.AddAuthorization();
        return services;
    }
}
