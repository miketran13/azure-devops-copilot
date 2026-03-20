using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DevOpsCopilot.Services;

/// <summary>
/// Validates the JWT app token from the Azure DevOps extension (SDK.getAppToken()).
/// This ensures requests to the backend originate from the published extension.
/// </summary>
public sealed class TokenValidationService
{
    private readonly string? _sharedSecret;
    private readonly ILogger<TokenValidationService> _logger;

    public TokenValidationService(IConfiguration configuration, ILogger<TokenValidationService> logger)
    {
        _sharedSecret = configuration["Extension:SharedSecret"];
        _logger = logger;
    }

    /// <summary>
    /// Validates the extension app token.
    /// Returns true if valid, false otherwise.
    /// In development mode (no shared secret configured), always returns true.
    /// </summary>
    public bool ValidateAppToken(string? appToken)
    {
        // If no shared secret is configured, skip validation (development mode)
        if (string.IsNullOrEmpty(_sharedSecret))
        {
            _logger.LogWarning("Extension:SharedSecret not configured — skipping app token validation (dev mode).");
            return true;
        }

        if (string.IsNullOrEmpty(appToken))
        {
            _logger.LogWarning("App token is missing.");
            return false;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_sharedSecret);

            tokenHandler.ValidateToken(appToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out _);

            return true;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            _logger.LogWarning(ex, "App token validation failed.");
            return false;
        }
    }

    /// <summary>
    /// Extracts the Bearer token from an Authorization header value.
    /// </summary>
    public static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader))
            return null;

        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authorizationHeader["Bearer ".Length..].Trim();

        return null;
    }
}
