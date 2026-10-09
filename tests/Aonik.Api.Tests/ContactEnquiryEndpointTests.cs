using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Infrastructure.Caching;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.Platform.Entities.ContactEnquiries;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Outbox;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public sealed class ContactEnquiryEndpointTests : IClassFixture<ContactEnquiryTestFactory>
{
    private const string PublicPath = "/v1/contact-enquiries";
    private const string AdminPath = "/v1/admin/contact-enquiries";
    private readonly ContactEnquiryTestFactory _factory;

    public ContactEnquiryEndpointTests(ContactEnquiryTestFactory factory)
    {
        _factory = factory;
        _factory.Services.GetRequiredService<FusionCacheInvalidationHandler>();
    }

    [Theory]
    [InlineData("order")]
    [InlineData("new")]
    [InlineData("dish")]
    [InlineData("delivery")]
    [InlineData("gift")]
    [InlineData("other")]
    public async Task Submit_Should_PersistTheWireFieldsAndTwoQueuedNotifications_WithoutClaimingDelivery(string topic)
    {
        var tenantId = await SeedAsync();
        using var client = Client(tenantId);
        var submission = Guid.NewGuid();
        using var form = Form(submission, topic: topic);

        using var response = await client.PostAsync(PublicPath, form);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Private(response);
        var receipt = (await response.Content.ReadFromJsonAsync<ContactEnquiryReceipt>())!;
        receipt.Id.Should().NotBeEmpty();
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("guest@example.test").And.NotContain("delivered").And.NotContain("queued@example.test");
        await using var scope = Scope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var enquiry = await db.Set<ContactEnquiry>().SingleAsync();
        enquiry.Id.Should().Be(receipt.Id);
        enquiry.SubmissionId.Should().Be(submission);
        enquiry.Email.Should().Be("guest@example.test");
        enquiry.Topic.Should().Be(topic);
        enquiry.OrderNumber.Should().Be(topic == "order" ? "UNVERIFIED-123" : null);
        var messages = await db.Set<OutboxMessage>().Where(x => x.TenantId == tenantId).ToListAsync();
        messages.Should().HaveCount(2);
        foreach (var message in messages)
            JsonSerializer.Serialize(message).Should().NotContain("guest@example.test").And.NotContain("Please help with my enquiry");
        (await db.Users.CountAsync()).Should().Be(0);
        (await db.Parties.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Retry_Should_ReturnTheDurableReceipt_AndRejectChangedReuseWithoutMoreEvents()
    {
        var tenantId = await SeedAsync();
        using var client = Client(tenantId);
        var submission = Guid.NewGuid();
        ContactEnquiryReceipt? first = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var form = Form(submission);
            using var response = await client.PostAsync(PublicPath, form);
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var receipt = await response.Content.ReadFromJsonAsync<ContactEnquiryReceipt>();
            if (first is null) first = receipt;
            else receipt.Should().Be(first);
        }
        using var changed = Form(submission, message: "A different message with the same reference");
        using var conflict = await client.PostAsync(PublicPath, changed);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Private(conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("contact.submission_conflict");
        await AssertCountsAsync(tenantId, 1, 0, 2);
    }

    [Fact]
    public async Task Images_Should_BePrivateStaffOnly_AndBoundToTheEnquiryAndTenant()
    {
        var tenantId = await SeedAsync();
        using var anonymous = Client(tenantId);
        using var form = Form(Guid.NewGuid());
        AddImage(form, [1, 2, 3], "meal.jpg");
        using var accepted = await anonymous.PostAsync(PublicPath, form);
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var receipt = (await accepted.Content.ReadFromJsonAsync<ContactEnquiryReceipt>())!;
        using var staff = await StaffAsync(tenantId);
        var list = (await staff.GetFromJsonAsync<PagedResult<ContactEnquirySummaryDto>>(AdminPath + "?page=1&pageSize=1&topic=other"))!;
        list.TotalCount.Should().Be(1);
        list.Items.Single().ImageCount.Should().Be(1);
        using var detailResponse = await staff.GetAsync($"{AdminPath}/{receipt.Id}");
        Private(detailResponse);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<ContactEnquiryDetailDto>())!;
        (await detailResponse.Content.ReadAsStringAsync()).Should().NotContain("storageKey").And.NotContain("StaffRecipient");
        var imageId = detail.Images.Single().Id;
        var path = $"{AdminPath}/{receipt.Id}/images/{imageId}";
        using var image = await staff.GetAsync(path);
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(image);
        image.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        image.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        (await image.Content.ReadAsByteArrayAsync()).Should().Equal(ContactEnquiryTestFactory.NormalizedImage);
        using var customer = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("PersonalUser").WithPermissions("Customers.Read"));
        using var staffWithoutPermission = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Operations"));
        foreach (var route in new[] { AdminPath, $"{AdminPath}/{receipt.Id}", path })
        {
            using var noAuth = await anonymous.GetAsync(route);
            noAuth.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            Private(noAuth);
            using var forbidden = await customer.GetAsync(route);
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            Private(forbidden);
            using var noPermission = await staffWithoutPermission.GetAsync(route);
            noPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            Private(noPermission);
        }
        using var foreign = await StaffAsync(await SeedAsync());
        using var foreignRead = await foreign.GetAsync(path);
        foreignRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Private(foreignRead);
        using var wrongEnquiry = await staff.GetAsync($"{AdminPath}/{Guid.NewGuid()}/images/{imageId}");
        wrongEnquiry.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var query in new[] { "page=0", "pageSize=101", "topic=unknown" })
            (await staff.GetAsync(AdminPath + "?" + query)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task InvalidFieldsAndImages_Should_ReturnSafeIndexedErrors_WithoutPartialAcceptance()
    {
        var tenantId = await SeedAsync();
        using var client = Client(tenantId);
        using var invalid = Form(Guid.NewGuid(), email: "not-an-email");
        using var fields = await client.PostAsync(PublicPath, invalid);
        fields.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(fields);
        (await fields.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fieldErrors").GetProperty("email").GetArrayLength().Should().BePositive();
        using var images = Form(Guid.NewGuid());
        AddImage(images, [1], "valid.jpg");
        AddImage(images, [13], "invalid.png", "image/png");
        using var files = await client.PostAsync(PublicPath, images);
        files.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(files);
        var problem = (await files.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imageProblems").EnumerateArray().Single();
        problem.GetProperty("index").GetInt32().Should().Be(1);
        problem.GetProperty("code").GetString().Should().Be("contact.image_invalid");
        await AssertCountsAsync(tenantId, 0, 0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableRouteOrProcessor_Should_ReturnRetryableFailureWithoutReceipt(bool scannerUnavailable)
    {
        var tenantId = await SeedAsync(configured: scannerUnavailable);
        using var client = Client(tenantId);
        using var form = Form(Guid.NewGuid());
        if (scannerUnavailable) AddImage(form, [42], "scan.jpg");
        using var response = await client.PostAsync(PublicPath, form);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter.Should().NotBeNull();
        Private(response);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("contact.unavailable");
        await AssertCountsAsync(tenantId, 0, 0, 0);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("missing-reference")]
    [InlineData("four-files")]
    public async Task Multipart_Should_RejectAmbiguousOrOversuppliedInputBeforePersistence(string invalid)
    {
        var tenantId = await SeedAsync();
        using var client = Client(tenantId);
        using var form = Form(invalid == "missing-reference" ? Guid.Empty : Guid.NewGuid());
        if (invalid is "duplicate" or "unknown") form.Add(new StringContent("value"), invalid == "duplicate" ? "email" : "recipient");
        if (invalid == "four-files") for (var index = 0; index < 4; index++) AddImage(form, [1], $"image{index}.jpg");
        using var response = await client.PostAsync(PublicPath, form);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(response);
        await AssertCountsAsync(tenantId, 0, 0, 0);
    }

    [Fact]
    public async Task Honeypot_Should_AvoidPersistenceOrNotifications_AndScannerGetRemainsPrivate()
    {
        var tenantId = await SeedAsync();
        using var client = Client(tenantId);
        using var form = Form(Guid.NewGuid());
        form.Add(new StringContent("spam.example.test"), "website");
        using var response = await client.PostAsync(PublicPath, form);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Private(response);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        using var scanner = await client.GetAsync(PublicPath);
        scanner.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        Private(scanner);
        await AssertCountsAsync(tenantId, 0, 0, 0);
    }

    [Fact]
    public async Task MeasuredBodyLimits_Should_RejectOversizedImagesWithoutTrustingContentLength()
    {
        var tenantId = await SeedAsync();
        using var client = Client(tenantId);
        using var form = Form(Guid.NewGuid());
        var oversized = new StreamContent(new NonSeekableReadStream(new byte[10 * 1024 * 1024 + 1]));
        oversized.Headers.ContentType = new("image/jpeg");
        form.Add(oversized, "images", "large.jpg");
        using var response = await client.PostAsync(PublicPath, form);
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        Private(response);
        await AssertCountsAsync(tenantId, 0, 0, 0);
    }

    [Fact]
    public async Task BodyLimits_Should_AcceptThreeMaximumSizeImages_AndRejectDeclaredAggregateOverflow()
    {
        var tenantId = await SeedAsync();
        using var client = Client(tenantId);
        using var form = Form(Guid.NewGuid());
        var maximum = new byte[10 * 1024 * 1024];
        for (var index = 0; index < 3; index++) AddImage(form, maximum, $"image{index}.jpg");
        using var accepted = await client.PostAsync(PublicPath, form);
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await AssertCountsAsync(tenantId, 1, 3, 2);

        var rejected = await _factory.Server.SendAsync(context =>
        {
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Method = "POST";
            context.Request.Path = PublicPath;
            context.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
            context.Request.ContentType = "multipart/form-data; boundary=never-read";
            context.Request.ContentLength = 32 * 1024 * 1024 + 1;
            context.Request.Body = new UnreadableStream();
        });
        rejected.Response.StatusCode.Should().Be(413);
        rejected.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        await AssertCountsAsync(tenantId, 1, 3, 2);
    }

    [Fact]
    public async Task ConcurrentProcessing_Should_RejectBeforeReadingAnotherMultipartBody()
    {
        var tenantId = await SeedAsync();
        using var client = Client(tenantId);
        var forms = Enumerable.Range(0, 4).Select(_ => Form(Guid.NewGuid())).ToList();
        _factory.ReleaseImages = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var form in forms) AddImage(form, [1], "blocked.jpg");
        var requests = forms.Select(form => client.PostAsync(PublicPath, form)).ToArray();
        try
        {
            for (var index = 0; index < 4; index++)
                (await _factory.BlockedImages.WaitAsync(TimeSpan.FromSeconds(20))).Should().BeTrue();
            var limited = await _factory.Server.SendAsync(context =>
            {
                context.Request.Scheme = "https";
                context.Request.Host = new HostString("localhost");
                context.Request.Method = "POST";
                context.Request.Path = PublicPath;
                context.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
                context.Request.ContentType = "multipart/form-data; boundary=never-read";
                context.Request.Body = new UnreadableStream();
            });
            limited.Response.StatusCode.Should().Be(429);
            limited.Response.Headers.RetryAfter.ToString().Should().Be("1");
            limited.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        }
        finally
        {
            _factory.ReleaseImages.TrySetResult();
            foreach (var response in await Task.WhenAll(requests))
            {
                response.StatusCode.Should().Be(HttpStatusCode.Accepted);
                response.Dispose();
            }
            foreach (var form in forms) form.Dispose();
            _factory.ReleaseImages = null;
        }
        await AssertCountsAsync(tenantId, 4, 4, 8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyTenantFailure_Should_RemainPrivate(bool unknownTenant)
    {
        using var client = unknownTenant ? Client(Guid.NewGuid()) : _factory.CreateClient();
        using var form = Form(Guid.NewGuid());
        using var response = await client.PostAsync(PublicPath, form);
        response.StatusCode.Should().Be(unknownTenant ? HttpStatusCode.NotFound : HttpStatusCode.Unauthorized);
        Private(response);
    }

    [Fact]
    public async Task RateLimit_Should_UseResolvedTenantAndTrustedClientAddress_WithoutTrustingArbitraryForwarding()
    {
        var tenantId = await SeedAsync();
        for (var index = 0; index < ContactEnquiryTestFactory.Permits; index++)
            (await SendFromIpAsync(tenantId, "198.51.100.8", $"203.0.113.{index + 1}")).Response.StatusCode.Should().Be(202);
        var limited = await SendFromIpAsync(tenantId, "::ffff:198.51.100.8", "203.0.113.99");
        limited.Response.StatusCode.Should().Be(429);
        limited.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        limited.Response.Headers.RetryAfter.ToString().Should().NotBeEmpty();
        (await SendFromIpAsync(tenantId, "198.51.100.9")).Response.StatusCode.Should().Be(202);
        (await SendFromIpAsync(await SeedAsync(), "198.51.100.8")).Response.StatusCode.Should().Be(202);
        for (var index = 0; index < ContactEnquiryTestFactory.Permits; index++)
            (await SendFromIpAsync(tenantId, "127.0.0.1", "203.0.113.100")).Response.StatusCode.Should().Be(202);
        (await SendFromIpAsync(tenantId, "127.0.0.1", "203.0.113.100")).Response.StatusCode.Should().Be(429);
        (await SendFromIpAsync(tenantId, "127.0.0.1", "203.0.113.101")).Response.StatusCode.Should().Be(202);
        await AssertCountsAsync(tenantId, 0, 0, 0);
    }

    private async Task<HttpContext> SendFromIpAsync(Guid tenantId, string remote, string? forwarded = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("bot"), "website");
        var bytes = await form.ReadAsByteArrayAsync();
        return await _factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Method = "POST";
            context.Request.Path = PublicPath;
            context.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
            if (forwarded is not null) context.Request.Headers["X-Forwarded-For"] = forwarded;
            context.Request.ContentType = form.Headers.ContentType!.ToString();
            context.Request.ContentLength = bytes.Length;
            context.Request.Body = new MemoryStream(bytes);
        });
    }

    private async Task<Guid> SeedAsync(bool configured = true)
    {
        var tenantId = Guid.NewGuid();
        await using var scope = Scope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Contact tests", Environment = "Testing",
            Status = TenantStatus.Active, DefaultCurrency = "GBP", SupportedCountriesJson = "[]" });
        if (configured) db.Settings.Add(new Setting { Key = ContactEnquirySettingNames.Configuration,
            Scope = SettingScope.Tenant, TenantId = tenantId,
            Value = JsonSerializer.Serialize(new ContactEnquiriesConfiguration(true,
                new[] { "order", "new", "dish", "delivery", "gift", "other" }.ToDictionary(topic => topic, _ => "queued@example.test"),
                "https://admin.example.test"), new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private async Task AssertCountsAsync(Guid tenantId, int enquiries, int images, int messages)
    {
        await using var scope = Scope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.Set<ContactEnquiry>().CountAsync()).Should().Be(enquiries);
        (await db.Set<ContactEnquiryAttachment>().CountAsync()).Should().Be(images);
        (await db.Set<OutboxMessage>().CountAsync(x => x.TenantId == tenantId)).Should().Be(messages);
    }

    private AsyncServiceScope Scope(Guid tenantId)
    {
        var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        return scope;
    }

    private HttpClient Client(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private Task<HttpClient> StaffAsync(Guid tenantId) => _factory.CreateAuthenticatedClientAsync(
        TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Operations").WithPermissions("Customers.Read"));

    private static MultipartFormDataContent Form(Guid submission, string topic = "other", string email = "guest@example.test",
        string message = "Please help with my enquiry")
    {
        var form = new MultipartFormDataContent();
        foreach (var pair in new Dictionary<string, string> { ["submission_id"] = submission.ToString(), ["name"] = "Guest Cook",
                     ["email"] = email, ["topic"] = topic, ["order_number"] = "UNVERIFIED-123", ["message"] = message })
            form.Add(new StringContent(pair.Value), pair.Key);
        return form;
    }

    private static void AddImage(MultipartFormDataContent form, byte[] bytes, string name, string contentType = "image/jpeg")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "images", name);
    }

    private static void Private(HttpResponseMessage response)
    {
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Body must not be read.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Body must not be read.");
    }
}
