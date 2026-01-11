using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Onboarding;
using Aonik.Application.Options;
using Aonik.Application.Services.Onboarding;
using Aonik.Domain.Identity;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.Party.Entities;
using Aonik.Infrastructure.Persistence;

namespace Aonik.Application.Tests.Onboarding;

public class OnboardingPolicyEvaluatorTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    [Fact]
    public async Task EvaluateAsync_ShouldReturnNoActions_WhenAllGatesSatisfied()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new AonikDbContext(options, tenantProvider);

        context.Tenants.Add(new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });

        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = "verified@example.com",
            Phone = "+15551234567",
            Status = "Active"
        });

        context.Parties.Add(new Party
        {
            PartyId = partyId,
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = "Verified User",
            Status = "Active"
        });

        context.PartyAddresses.Add(new PartyAddress
        {
            PartyAddressId = Guid.NewGuid(),
            PartyId = partyId,
            Type = "Home",
            Line1 = "123 Main St",
            City = "Seattle",
            Postcode = "98101",
            Country = "US"
        });

        context.PartyContacts.Add(new PartyContact
        {
            PartyContactId = Guid.NewGuid(),
            PartyId = partyId,
            Type = "Email",
            Value = "verified@example.com",
            IsPrimary = true
        });

        context.UserParties.Add(new UserParty
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            LinkType = "Individual"
        });

        context.VerificationChallenges.AddRange(
            new VerificationChallenge
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = VerificationChannel.Email,
                Target = "verified@example.com",
                CodeHash = "hash",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                AttemptCount = 0,
                Status = VerificationStatus.Verified
            },
            new VerificationChallenge
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = VerificationChannel.Sms,
                Target = "+15551234567",
                CodeHash = "hash",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                AttemptCount = 0,
                Status = VerificationStatus.Verified
            });

        await context.SaveChangesAsync();

        var evaluator = new OnboardingPolicyEvaluator(
            context,
            Microsoft.Extensions.Options.Options.Create(new OnboardingPolicyOptions
            {
                RequireEmailVerified = true,
                RequirePhoneVerified = true,
                RequireProfileComplete = true
            }));

        // Act
        var snapshot = await evaluator.EvaluateAsync(userId, CancellationToken.None);

        // Assert
        snapshot.NextActions.Should().BeEmpty();
        snapshot.Gates.Should().HaveCount(3);
        snapshot.Gates.Should().OnlyContain(gate => gate.IsSatisfied);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldReturnActions_WhenRequirementsMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new AonikDbContext(options, tenantProvider);

        context.Tenants.Add(new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });

        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = "pending@example.com",
            Phone = "+15550001111",
            Status = "Active"
        });

        context.Parties.Add(new Party
        {
            PartyId = partyId,
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = "Pending User",
            Status = "Active"
        });

        context.UserParties.Add(new UserParty
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            LinkType = "Individual"
        });

        await context.SaveChangesAsync();

        var evaluator = new OnboardingPolicyEvaluator(
            context,
            Microsoft.Extensions.Options.Options.Create(new OnboardingPolicyOptions
            {
                RequireEmailVerified = true,
                RequirePhoneVerified = true,
                RequireProfileComplete = true,
                EmailVerifiedActions = new List<string> { "VerifyEmail" },
                PhoneVerifiedActions = new List<string> { "VerifyPhone" },
                ProfileCompleteActions = new List<string> { "CompleteProfile" }
            }));

        // Act
        var snapshot = await evaluator.EvaluateAsync(userId, CancellationToken.None);

        // Assert
        snapshot.NextActions.Should().BeEquivalentTo(new[]
        {
            "VerifyEmail",
            "VerifyPhone",
            "CompleteProfile"
        });

        var emailGate = snapshot.Gates.Single(gate => gate.Gate == OnboardingGate.EmailVerified);
        emailGate.IsSatisfied.Should().BeFalse();

        var phoneGate = snapshot.Gates.Single(gate => gate.Gate == OnboardingGate.PhoneVerified);
        phoneGate.IsSatisfied.Should().BeFalse();

        var profileGate = snapshot.Gates.Single(gate => gate.Gate == OnboardingGate.ProfileComplete);
        profileGate.IsSatisfied.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_ShouldOnlyRequireEnabledGates()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new AonikDbContext(options, tenantProvider);

        context.Tenants.Add(new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });

        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Email = "pending@example.com",
            Phone = "+15550001111",
            Status = "Active"
        });

        context.Parties.Add(new Party
        {
            PartyId = partyId,
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = "Pending User",
            Status = "Active"
        });

        context.UserParties.Add(new UserParty
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            LinkType = "Individual"
        });

        await context.SaveChangesAsync();

        var evaluator = new OnboardingPolicyEvaluator(
            context,
            Microsoft.Extensions.Options.Options.Create(new OnboardingPolicyOptions
            {
                RequireEmailVerified = false,
                RequirePhoneVerified = true,
                RequireProfileComplete = false,
                EmailVerifiedActions = new List<string> { "VerifyEmail" },
                PhoneVerifiedActions = new List<string> { "VerifyPhone" },
                ProfileCompleteActions = new List<string> { "CompleteProfile" }
            }));

        // Act
        var snapshot = await evaluator.EvaluateAsync(userId, CancellationToken.None);

        // Assert
        snapshot.NextActions.Should().BeEquivalentTo(new[] { "VerifyPhone" });

        snapshot.Gates.Single(gate => gate.Gate == OnboardingGate.EmailVerified).IsRequired.Should().BeFalse();
        snapshot.Gates.Single(gate => gate.Gate == OnboardingGate.PhoneVerified).IsRequired.Should().BeTrue();
        snapshot.Gates.Single(gate => gate.Gate == OnboardingGate.ProfileComplete).IsRequired.Should().BeFalse();
    }
}
