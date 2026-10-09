using System.Net;
using System.Net.Http.Json;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class CustomerAddressEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Path = "/profiles/customers/me/addresses";

    [Fact]
    public async Task PersonalUser_Should_CreateEditSelectAndDeleteOwnedAddresses_WithOneDefault()
    {
        var customer = await CustomerAsync();
        using var client = customer.Client;
        var book = await ReadAsync(client);
        book.Addresses.Should().BeEmpty();
        book.DefaultAddressId.Should().BeNull();

        book = await WriteAsync(client, HttpMethod.Post, Path,
            Address(book.Version) with { Line1 = " 12 Saved Street ", Line3 = "Third address line", Country = " gb " }, HttpStatusCode.Created);

        var first = book.Addresses.Should().ContainSingle().Subject;
        first.Line1.Should().Be("12 Saved Street");
        first.Line3.Should().Be("Third address line");
        first.Country.Should().Be("GB");
        first.IsDefault.Should().BeTrue();
        book.DefaultAddressId.Should().Be(first.Id);
        book = await WriteAsync(client, HttpMethod.Post, Path, Address(book.Version) with { Line1 = "34 Other Street" }, HttpStatusCode.Created);
        var second = book.Addresses.Single(address => address.Id != first.Id);
        second.IsDefault.Should().BeFalse();
        book.DefaultAddressId.Should().Be(first.Id);

        book = await WriteAsync(client, HttpMethod.Put, $"{Path}/{second.Id}",
            Address(book.Version) with { Line1 = "56 Updated Street", Line2 = "Flat 2", Line3 = "Top floor", Country = "FR" });
        book.Addresses.Single(address => address.Id == second.Id).Should().BeEquivalentTo(
            new CustomerAddressDto(second.Id, "Shipping", "56 Updated Street", "Flat 2", "Top floor", "London", null, "SW1A 1AA", "FR", false));
        book = await WriteAsync(client, HttpMethod.Put, $"{Path}/{second.Id}/default", new CustomerAddressVersionRequest(book.Version));
        book.DefaultAddressId.Should().Be(second.Id);
        book.Addresses.Should().ContainSingle(address => address.IsDefault);

        book = await WriteAsync(client, HttpMethod.Delete, $"{Path}/{second.Id}", new CustomerAddressVersionRequest(book.Version));
        book.DefaultAddressId.Should().Be(first.Id);
        book.Addresses.Should().ContainSingle().Which.IsDefault.Should().BeTrue();
        book = await WriteAsync(client, HttpMethod.Delete, $"{Path}/{first.Id}", new CustomerAddressVersionRequest(book.Version));
        book.Addresses.Should().BeEmpty();
        book.DefaultAddressId.Should().BeNull();
        (await ReadAsync(client)).Should().BeEquivalentTo(book);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("stale")]
    public async Task Writes_Should_RejectMissingMalformedOrStaleBookVersion_WithoutEffects(string reason)
    {
        var customer = await CustomerAsync();
        using var client = customer.Client;
        var book = await WriteAsync(client, HttpMethod.Post, Path, Address((await ReadAsync(client)).Version), HttpStatusCode.Created);
        var addressId = book.DefaultAddressId!.Value;
        var version = reason switch { "missing" => null, "malformed" => "invalid-base64", _ => book.Version };
        if (reason == "stale") await SetPartyVersionAsync(customer, [8, 7, 6, 5, 4, 3, 2, 1]);

        foreach (var (method, path, body) in new (HttpMethod, string, object)[]
        {
            (HttpMethod.Post, Path, Address(version)),
            (HttpMethod.Put, $"{Path}/{addressId}", Address(version) with { Line1 = "Rejected edit" }),
            (HttpMethod.Delete, $"{Path}/{addressId}", new CustomerAddressVersionRequest(version)),
            (HttpMethod.Put, $"{Path}/{addressId}/default", new CustomerAddressVersionRequest(version))
        })
        {
            using var response = await SendAsync(client, method, path, body);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            AssertPrivate(response);
            (await response.Content.ReadAsStringAsync()).Should().Contain("concurrency_conflict");
        }

        var unchanged = await ReadAsync(client);
        unchanged.Addresses.Should().BeEquivalentTo(book.Addresses);
        unchanged.DefaultAddressId.Should().Be(addressId);
    }

    [Theory]
    [InlineData("same-tenant")]
    [InlineData("cross-tenant")]
    [InlineData("deleted")]
    [InlineData("missing")]
    public async Task ForeignDeletedAndMissingAddresses_Should_ReturnTheSamePrivate404(string target)
    {
        var customer = await CustomerAsync();
        using var client = customer.Client;
        var other = await CustomerAsync(target == "cross-tenant" ? Guid.NewGuid() : customer.TenantId);
        using var otherClient = other.Client;
        var own = await WriteAsync(client, HttpMethod.Post, Path, Address((await ReadAsync(client)).Version), HttpStatusCode.Created);
        var foreign = await WriteAsync(otherClient, HttpMethod.Post, Path,
            Address((await ReadAsync(otherClient)).Version) with { Line1 = "Private foreign address" }, HttpStatusCode.Created);
        var addressId = target switch { "deleted" => own.DefaultAddressId!.Value, "missing" => Guid.NewGuid(), _ => foreign.DefaultAddressId!.Value };
        if (target == "deleted")
            own = await WriteAsync(client, HttpMethod.Delete, $"{Path}/{addressId}", new CustomerAddressVersionRequest(own.Version));

        foreach (var (method, path, body) in new (HttpMethod, string, object)[]
        {
            (HttpMethod.Put, $"{Path}/{addressId}", Address(own.Version)),
            (HttpMethod.Delete, $"{Path}/{addressId}", new CustomerAddressVersionRequest(own.Version)),
            (HttpMethod.Put, $"{Path}/{addressId}/default", new CustomerAddressVersionRequest(own.Version))
        })
        {
            using var response = await SendAsync(client, method, path, body);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            AssertPrivate(response);
            (await response.Content.ReadAsStringAsync()).Should().NotContain("Private foreign address");
        }

        (await ReadAsync(client)).Should().BeEquivalentTo(own);
        (await ReadAsync(otherClient)).Should().BeEquivalentTo(foreign);
    }

    [Fact]
    public async Task AnonymousRequests_Should_BeDenied_AndAllResponsesPrivate()
    {
        var customer = await CustomerAsync();
        using var customerClient = customer.Client;
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", customer.TenantId.ToString());
        var id = Guid.NewGuid();
        using var read = await anonymous.GetAsync(Path);
        read.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AssertPrivate(read);
        foreach (var (method, path, body) in new (HttpMethod, string, object)[]
        {
            (HttpMethod.Post, Path, Address(null)),
            (HttpMethod.Put, $"{Path}/{id}", Address(null)),
            (HttpMethod.Delete, $"{Path}/{id}", new CustomerAddressVersionRequest()),
            (HttpMethod.Put, $"{Path}/{id}/default", new CustomerAddressVersionRequest())
        })
        {
            using var response = await SendAsync(anonymous, method, path, body);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            AssertPrivate(response);
        }
    }

    [Fact]
    public async Task PersonalUser_Should_NeedThePermissionForEachOperation()
    {
        var customer = await CustomerAsync(permissions: ["UserInfo.Read"]);
        using var reader = customer.Client;
        var book = await ReadAsync(reader);
        foreach (var (method, path, body) in new (HttpMethod, string, object)[]
        {
            (HttpMethod.Post, Path, Address(book.Version)),
            (HttpMethod.Put, $"{Path}/{Guid.NewGuid()}", Address(book.Version)),
            (HttpMethod.Delete, $"{Path}/{Guid.NewGuid()}", new CustomerAddressVersionRequest(book.Version)),
            (HttpMethod.Put, $"{Path}/{Guid.NewGuid()}/default", new CustomerAddressVersionRequest(book.Version))
        })
        {
            using var response = await SendAsync(reader, method, path, body);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            AssertPrivate(response);
        }
        var writer = await CustomerAsync(permissions: ["UserInfo.Update"]);
        using var writerClient = writer.Client;
        using var deniedRead = await writerClient.GetAsync(Path);
        deniedRead.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AssertPrivate(deniedRead);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("foreign-link")]
    public async Task MissingLiveOwnedProfile_Should_Return404_WithoutProvisioning(string reason)
    {
        var customer = await CustomerAsync(linkParty: reason != "missing");
        using var client = customer.Client;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = customer.TenantId;
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            if (reason == "deleted") (await db.Parties.SingleAsync(party => party.Id == customer.PartyId)).IsDeleted = true;
            if (reason == "foreign-link")
            {
                var foreign = new Party { TenantId = Guid.NewGuid(), DisplayName = "Private foreign party", PartyType = "Individual", Status = "Active" };
                db.Parties.Add(foreign);
                (await db.UserParties.SingleAsync(link => link.UserId == customer.UserId)).PartyId = foreign.Id;
            }
            await db.SaveChangesAsync();
        }

        using var read = await client.GetAsync(Path);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertPrivate(read);
        using var create = await client.PostAsJsonAsync(Path, Address(Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8])));
        create.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertPrivate(create);
        (await create.Content.ReadAsStringAsync()).Should().NotContain("Private foreign party");
    }

    [Theory]
    [InlineData("line1")]
    [InlineData("country")]
    [InlineData("control")]
    public async Task InvalidAddress_Should_Return422_WithoutSaving(string reason)
    {
        var customer = await CustomerAsync();
        using var client = customer.Client;
        var before = await ReadAsync(client);
        var command = Address(before.Version);
        command = reason switch
        {
            "line1" => command with { Line1 = "   " },
            "country" => command with { Country = "GBR" },
            _ => command with { City = "London\nHidden" }
        };

        using var response = await client.PostAsJsonAsync(Path, command);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        AssertPrivate(response);
        (await ReadAsync(client)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task AddressWrite_Should_RejectBodyOwnershipSelectors()
    {
        var customer = await CustomerAsync();
        using var client = customer.Client;
        var book = await ReadAsync(client);

        using var response = await client.PostAsJsonAsync(Path, new
        {
            type = "Shipping", line1 = "12 Saved Street", line2 = (string?)null, line3 = (string?)null,
            city = "London", state = (string?)null, postcode = "SW1A 1AA", country = "GB", expectedVersion = book.Version,
            partyId = Guid.NewGuid(), tenantId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        AssertPrivate(response);
        (await ReadAsync(client)).Should().BeEquivalentTo(book);
    }

    private async Task<Customer> CustomerAsync(Guid? tenantId = null, string[]? permissions = null, bool linkParty = true)
    {
        var tenant = tenantId ?? Guid.NewGuid();
        var auth = TestAuthOptions.Create().WithTenant(tenant).WithRoles("PersonalUser")
            .WithPermissions(permissions ?? ["UserInfo.Read", "UserInfo.Update"]);
        var client = await factory.CreateAuthenticatedClientAsync(auth);
        var party = linkParty ? await WorkspaceTestSeeding.SeedPartyAsync(factory, tenant, auth.UserId, "Address customer") : Guid.Empty;
        var result = new Customer(tenant, auth.UserId, party, client);
        // InMemory transports a seeded token; native row-version advancement/races are tested on SQL Server.
        if (linkParty) await SetPartyVersionAsync(result, [1, 2, 3, 4, 5, 6, 7, 8]);
        return result;
    }

    private async Task SetPartyVersionAsync(Customer customer, byte[] version)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = customer.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await db.Parties.SingleAsync(party => party.Id == customer.PartyId)).RowVersion = version;
        await db.SaveChangesAsync();
    }

    private static CustomerAddressWrite Address(string? version) => new("Shipping", "12 Saved Street", null, null,
        "London", null, "SW1A 1AA", "GB", version);

    private static async Task<CustomerAddressBookDto> ReadAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivate(response);
        return (await response.Content.ReadFromJsonAsync<CustomerAddressBookDto>())!;
    }

    private static async Task<CustomerAddressBookDto> WriteAsync(HttpClient client, HttpMethod method, string path, object body,
        HttpStatusCode expectedStatus = HttpStatusCode.OK)
    {
        using var response = await SendAsync(client, method, path, body);
        response.StatusCode.Should().Be(expectedStatus);
        AssertPrivate(response);
        return (await response.Content.ReadFromJsonAsync<CustomerAddressBookDto>())!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        return await client.SendAsync(request);
    }

    private static void AssertPrivate(HttpResponseMessage response)
    {
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    private sealed record Customer(Guid TenantId, Guid UserId, Guid PartyId, HttpClient Client);
}
