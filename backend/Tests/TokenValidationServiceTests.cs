using DevOpsCopilot.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DevOpsCopilot.Tests;

public class TokenValidationServiceTests
{
    private readonly Mock<ILogger<TokenValidationService>> _loggerMock = new();

    [Fact]
    public void ValidateAppToken_NoSharedSecret_ReturnsTrue()
    {
        // Arrange: no shared secret = dev mode
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var service = new TokenValidationService(config, _loggerMock.Object);

        // Act & Assert
        service.ValidateAppToken("any-token").Should().BeTrue();
        service.ValidateAppToken(null).Should().BeTrue();
    }

    [Fact]
    public void ValidateAppToken_WithSharedSecret_NullToken_ReturnsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Extension:SharedSecret"] = "my-secret-key-that-is-long-enough-for-hmac"
            })
            .Build();

        var service = new TokenValidationService(config, _loggerMock.Object);

        service.ValidateAppToken(null).Should().BeFalse();
        service.ValidateAppToken(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void ValidateAppToken_WithSharedSecret_InvalidToken_ReturnsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Extension:SharedSecret"] = "my-secret-key-that-is-long-enough-for-hmac"
            })
            .Build();

        var service = new TokenValidationService(config, _loggerMock.Object);

        service.ValidateAppToken("invalid-jwt-token").Should().BeFalse();
    }

    [Theory]
    [InlineData("Bearer abc123", "abc123")]
    [InlineData("bearer XYZ", "XYZ")]
    [InlineData("Bearer  spaced ", "spaced")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Basic abc", null)]
    public void ExtractBearerToken_ExtractsCorrectly(string? header, string? expected)
    {
        TokenValidationService.ExtractBearerToken(header).Should().Be(expected);
    }
}
