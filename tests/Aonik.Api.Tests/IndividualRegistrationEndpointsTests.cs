using System.Net;
using System.Net.Http.Json;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;

namespace Aonik.Api.Tests;

public class IndividualRegistrationEndpointsTests
{
    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    public async Task Register_ShouldRejectAdmissionWithoutCreatingIdentity_WhenOnlyBodySpecifiesTenant(string status)
    {
        // Arrange: use the actual anonymous pipeline without a tenant header or JWT.
        var idpFactory = new UnexpectedIdpFactory();
        await using var baseFactory = new CustomWebApplicationFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIdpUserProvisionerFactory>();
            services.AddSingleton<IIdpUserProvisionerFactory>(idpFactory);
        }));
        using var client = factory.CreateClient();
        var tenantId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            context.Tenants.Add(new Tenant { Id = tenantId, Name = "Registration boundary", Status = status });
            await context.SaveChangesAsync();
        }

        // Act
        var response = await client.PostAsJsonAsync("/v1/registrations/individual", new
        {
            tenantId, registrationCountry = "GB", firstName = "Synthetic", lastName = "Creator",
            email = "creator@example.com", password = "Synthetic-test-only-123!"
        });

        // Assert: this is a deliberate admission denial, not missing routing or a 500 after an IdP write.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(status == TenantStatus.Active ? "invite" : "not accepting registrations");
        idpFactory.WasCalled.Should().BeFalse();
    }

    private sealed class UnexpectedIdpFactory : IIdpUserProvisionerFactory
    {
        public bool WasCalled { get; private set; }

        public IIdpUserProvisioner GetProvisioner(string provider)
        {
            WasCalled = true;
            throw new InvalidOperationException("An inadmissible registration must not reach the identity provider.");
        }
    }
}
