using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Models.Onboarding;
using Aonik.Platform.Contracts.Models.Registration;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Onboarding;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Services.Registration;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Application.Tests.Identity;

public class RegistrationAdmissionTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("other-tenant")]
    [InlineData("linked")]
    [InlineData("inactive-user")]
    [InlineData("inactive-tenant")]
    public async Task RegisterIndividualAsync_ShouldRejectBeforeIdpCreation_WhenAdmissionIsMissing(string scenario)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Private publishing test",
            Status = scenario == "inactive-tenant" ? TenantStatus.Suspended : TenantStatus.Active
        });
        if (scenario != "missing")
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = scenario == "other-tenant" ? Guid.NewGuid() : tenantId,
                Email = "creator@example.com",
                ExternalIssuer = scenario == "linked" ? "https://identity.example/" : BootstrapIdentityConstants.PendingOwnerIssuer,
                ExternalSubject = "invite:creator@example.com",
                Status = scenario == "inactive-user" ? "Inactive" : "Active"
            });
        }
        await context.SaveChangesAsync();
        var idp = new Mock<IIdpUserProvisioner>(MockBehavior.Strict);
        var service = CreateService(context, idp);

        // Act
        var register = () => service.RegisterIndividualAsync(Request(tenantId));

        // Assert
        await register.Should().ThrowAsync<TenantAccessDeniedException>();
        idp.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RegisterIndividualAsync_ShouldComplete_WhenActiveTenantHasMatchingInvitation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        context.Tenants.Add(new Tenant { Id = tenantId, Name = "Private publishing test" });
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Email = "Creator@Example.COM",
            ExternalIssuer = BootstrapIdentityConstants.PendingOwnerIssuer,
            ExternalSubject = "invite:creator@example.com", Status = "Active"
        });
        await context.SaveChangesAsync();
        var idp = new Mock<IIdpUserProvisioner>(MockBehavior.Strict);
        idp.Setup(provisioner => provisioner.CreateUserAsync(It.IsAny<IdpUserRegistration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalIdentityResult("https://identity.example/", "creator-1", null));
        var service = CreateService(context, idp);

        // Act
        var result = await service.RegisterIndividualAsync(Request(tenantId));

        // Assert
        result.UserId.Should().NotBeEmpty();
        result.PartyId.Should().NotBeEmpty();
        result.Onboarding.Should().NotBeNull();
        idp.Verify(provisioner => provisioner.CreateUserAsync(
            It.Is<IdpUserRegistration>(registration => registration.Email == "creator@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IndividualRegistrationRequest Request(Guid tenantId) =>
        new(tenantId, "GB", null, "Synthetic", "Creator", "creator@example.com", null, "Synthetic-test-only-123!");

    private static PlatformDbContext CreateContext(Guid tenantId) => new(
        new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase($"Registration_{Guid.NewGuid()}").Options,
        new TestTenantProvider(tenantId));

    private static RegistrationService CreateService(PlatformDbContext context, Mock<IIdpUserProvisioner> idp)
    {
        var factory = new Mock<IIdpUserProvisionerFactory>();
        factory.Setup(item => item.GetProvisioner(It.IsAny<string>())).Returns(idp.Object);
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var provisioning = new Mock<IUserProvisioningService>();
        provisioning.Setup(item => item.EnsureUserAndCustomerAsync(It.IsAny<IExternalIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProvisioningResult(userId, partyId, false, true, true));
        var onboarding = new Mock<IOnboardingPolicyEvaluator>();
        onboarding.Setup(item => item.EvaluateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSnapshot(userId, partyId, [], []));
        return new RegistrationService(Mock.Of<ISettingProvider>(), factory.Object, provisioning.Object,
            Mock.Of<IUserProfileService>(), Mock.Of<IVerificationService>(), onboarding.Object,
            Mock.Of<IPersonalProfileProvisioner>(), context, NullLogger<RegistrationService>.Instance);
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid currentTenantId)
        {
            currentTenantId = tenantId;
            return true;
        }
    }
}
