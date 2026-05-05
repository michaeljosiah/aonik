using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class CustomerDataExportImportEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public CustomerDataExportImportEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ─── Export ─────────────────────────────────────────────

    [Fact]
    public async Task ExportCustomerData_Should_ReturnJsonFileDownload()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await SeedCustomerAsync(tenantId, userId, partyId);
        var client = await CreateClientAsync(tenantId, userId);

        // Act
        var response = await client.GetAsync($"/admin/customers/{partyId}/export");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentDisposition.Should().NotBeNull();
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain(partyId.ToString("N"));

        var json = await response.Content.ReadAsStringAsync();
        var bundle = JsonSerializer.Deserialize<CustomerDataBundle>(json, JsonOptions);

        bundle.Should().NotBeNull();
        bundle!.Version.Should().Be("1.0");
        bundle.RootPartyId.Should().Be(partyId);
        bundle.SourceTenantId.Should().Be(tenantId);
        bundle.Data.Should().ContainKey("Party");
        bundle.EntityCounts.Should().ContainKey("Party");
        bundle.EntityCounts["Party"].Should().Be(1);
    }

    [Fact]
    public async Task ExportCustomerData_Should_Return404_When_PartyNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var nonExistentPartyId = Guid.NewGuid();

        var client = await CreateClientAsync(tenantId, userId);

        // Act
        var response = await client.GetAsync($"/admin/customers/{nonExistentPartyId}/export");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportCustomerData_Should_IncludeProfileData()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await SeedCustomerWithProfileAsync(tenantId, userId, partyId);
        var client = await CreateClientAsync(tenantId, userId);

        // Act
        var response = await client.GetAsync($"/admin/customers/{partyId}/export");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var bundle = JsonSerializer.Deserialize<CustomerDataBundle>(json, JsonOptions);

        bundle.Should().NotBeNull();
        bundle!.Data.Should().ContainKey("PersonProfile");
        bundle.EntityCounts["PersonProfile"].Should().Be(1);
    }

    // ─── Import ─────────────────────────────────────────────

    [Fact]
    public async Task ImportCustomerData_Should_CreateNewCustomer()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var originalPartyId = Guid.NewGuid();

        await SeedCustomerWithProfileAsync(tenantId, userId, originalPartyId);
        var client = await CreateClientAsync(tenantId, userId, includeWritePermission: true);

        // First export the customer
        var exportResponse = await client.GetAsync($"/admin/customers/{originalPartyId}/export");
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var exportJson = await exportResponse.Content.ReadAsStringAsync();
        var bundle = JsonSerializer.Deserialize<CustomerDataBundle>(exportJson, JsonOptions);
        bundle.Should().NotBeNull();

        // Act — import the bundle
        var importPayload = new { bundle, conflictMode = "skip" };
        var importResponse = await client.PostAsJsonAsync("/admin/customers/import", importPayload, JsonOptions);

        // Assert
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var importResult = await importResponse.Content.ReadFromJsonAsync<ImportResponse>(JsonOptions);
        importResult.Should().NotBeNull();
        importResult!.NewPartyId.Should().NotBeEmpty();
        importResult.NewPartyId.Should().NotBe(originalPartyId);
        importResult.TotalEntities.Should().BeGreaterThan(0);
        importResult.EntityCounts.Should().ContainKey("Party");

        // Verify the new party exists in the database
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var newParty = await dbContext.Parties.FindAsync(importResult.NewPartyId);
        newParty.Should().NotBeNull();
        newParty!.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task ImportCustomerData_Should_ReturnValidationError_When_BundleIsNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, userId, includeWritePermission: true);

        // Act
        var importPayload = new { bundle = (object?)null, conflictMode = "fail" };
        var importResponse = await client.PostAsJsonAsync("/admin/customers/import", importPayload, JsonOptions);

        // Assert — null Bundle is rejected by the FluentValidation
        // Validator<ImportCustomerDataRequest>, surfaced as 422 Unprocessable
        // Content per the global FastEndpoints ErrorOptions.StatusCode.
        importResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── Round-Trip ─────────────────────────────────────────

    [Fact]
    public async Task ExportThenImport_Should_RoundTrip_WithNewIds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await SeedCustomerWithProfileAsync(tenantId, userId, partyId);
        var client = await CreateClientAsync(tenantId, userId, includeWritePermission: true);

        // Export
        var exportResponse = await client.GetAsync($"/admin/customers/{partyId}/export");
        var exportJson = await exportResponse.Content.ReadAsStringAsync();
        var bundle = JsonSerializer.Deserialize<CustomerDataBundle>(exportJson, JsonOptions);

        // Import
        var importPayload = new { bundle, conflictMode = "skip" };
        var importResponse = await client.PostAsJsonAsync("/admin/customers/import", importPayload, JsonOptions);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var importResult = await importResponse.Content.ReadFromJsonAsync<ImportResponse>(JsonOptions);
        importResult.Should().NotBeNull();

        // Re-export the imported customer
        var reExportResponse = await client.GetAsync($"/admin/customers/{importResult!.NewPartyId}/export");
        reExportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reExportJson = await reExportResponse.Content.ReadAsStringAsync();
        var reExportBundle = JsonSerializer.Deserialize<CustomerDataBundle>(reExportJson, JsonOptions);

        // Verify both bundles have the same entity types and counts
        reExportBundle.Should().NotBeNull();
        reExportBundle!.Data.Should().ContainKey("Party");
        reExportBundle.EntityCounts["Party"].Should().Be(1);

        // The IDs should be different (remapped)
        reExportBundle.RootPartyId.Should().NotBe(partyId);
        reExportBundle.RootPartyId.Should().Be(importResult.NewPartyId);
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<HttpClient> CreateClientAsync(
        Guid tenantId,
        Guid userId,
        bool includeWritePermission = false)
    {
        var permissions = new List<string> { "Customers.Read" };
        if (includeWritePermission)
            permissions.Add("Customers.Write");

        var options = TestAuthOptions.Create()
            .WithTenant(tenantId)
            .WithRoles("TenantAdmin")
            .WithPermissions(permissions.ToArray());
        options.UserId = userId;

        return await _factory.CreateAuthenticatedClientAsync(options);
    }

    private async Task SeedCustomerAsync(Guid tenantId, Guid userId, Guid partyId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        dbContext.Parties.Add(new Party
        {
            Id = partyId,
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = "Test Customer",
            Status = "Active",
        });

        dbContext.UserParties.Add(new UserParty
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            LinkType = "Owner",
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedCustomerWithProfileAsync(Guid tenantId, Guid userId, Guid partyId)
    {
        await SeedCustomerAsync(tenantId, userId, partyId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        dbContext.PersonProfiles.Add(new PersonProfile
        {
            Id = Guid.NewGuid(),
            PartyId = partyId,
            FirstName = "Test",
            LastName = "Customer",
            Dob = new DateTime(1990, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            IdvStatus = "NotStarted",
        });

        await dbContext.SaveChangesAsync();
    }

    private record ImportResponse(
        Guid NewPartyId,
        Dictionary<string, int> EntityCounts,
        int TotalEntities,
        List<string> Warnings);
}
