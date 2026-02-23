using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Aonik.SharedKernel.Abstractions.Multitenancy;


using Aonik.Application.Services.Identity;
using Aonik.Api.Contracts.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Infrastructure.Persistence;

namespace Aonik.Api.Tests;

public class VerificationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VerificationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StartEmailVerification_ReturnsChallenge()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithPermissions("Users.Read", "UserInfo.Update")
                .WithRoles("PersonalUser"));



        var request = new StartPhoneVerificationRequest("+15551234567");

        // Act
        var response = await client.PostAsJsonAsync("/v1/verifications/phone/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<VerificationChallengeResponse>();
        result.Should().NotBeNull();
        result!.ChallengeId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConfirmEmailVerification_ReturnsVerified()
    {
        // Arrange
        var authOptions = TestAuthOptions.Create()
            .WithPermissions("Users.Read", "UserInfo.Update")
            .WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(authOptions);

        const string code = "123456";
        const string email = "verified@example.com";

        await SeedChallengeAsync(
            authOptions,
            VerificationChannel.Email,
            email,
            code);

        var request = new ConfirmEmailVerificationRequest(email, code);

        // Act
        var response = await client.PostAsJsonAsync("/v1/verifications/email/confirm", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<VerificationConfirmationResponse>();
        result.Should().NotBeNull();
        result!.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmPhoneVerification_ReturnsVerified()
    {
        // Arrange
        var authOptions = TestAuthOptions.Create()
            .WithPermissions("Users.Read", "UserInfo.Update")
            .WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(authOptions);

        const string code = "654321";
        const string phone = "+15550001111";

        await SeedChallengeAsync(
            authOptions,
            VerificationChannel.Sms,
            phone,
            code);

        var request = new ConfirmPhoneVerificationRequest(phone, code);

        // Act
        var response = await client.PostAsJsonAsync("/v1/verifications/phone/confirm", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<VerificationConfirmationResponse>();
        result.Should().NotBeNull();
        result!.IsVerified.Should().BeTrue();
    }

    private async Task SeedChallengeAsync(
        TestAuthOptions options,
        VerificationChannel channel,
        string target,
        string code)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var verificationOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<VerificationOptions>>()
            .Value;

        tenantContext.TenantId = options.TenantId;


        var challenge = new VerificationChallenge
        {
            TenantId = options.TenantId!.Value,
            UserId = options.UserId,
            Channel = channel,
            Target = channel == VerificationChannel.Email ? target.Trim().ToLowerInvariant() : target.Trim(),
            CodeHash = HashCode(code, verificationOptions.HashKey),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            AttemptCount = 0,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.VerificationChallenges.Add(challenge);
        await dbContext.SaveChangesAsync();
    }

    private static string HashCode(string code, string hashKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hashKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash);
    }
}
