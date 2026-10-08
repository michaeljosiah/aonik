using Aonik.Infrastructure.Notifications;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Notifications;
using Aonik.Platform.Services.Seeding;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.Notifications;

public sealed class TemplatedEmailSenderTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task SendAsync_Should_RenderTheReceiptAndEscapeAuthoredValues_WithPublishedBranding()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var transport = new RecordingEmailSender();
        var profile = new PublicBusinessProfileDto("Kitchen & <Friends>",
            LogoUrl: "https://example.test/logo.png?variant=light&size=2",
            Website: "https://example.test/?from=email&source=order",
            Contact: new("support&orders@example.test", "01234 <567890>"));
        var sender = CreateSender(context, transport, profile);
        var model = ReceiptModel();
        model["brand"] = new Dictionary<string, object?> { ["display_name"] = "Spoofed brand" };

        await sender.SendAsync(new(TransactionalEmailTemplateNames.OrderConfirmation, "buyer@example.test", model));

        var message = transport.Messages.Should().ContainSingle().Which;
        message.To.Should().Be("buyer@example.test");
        message.From.Should().BeNull();
        message.IsHtml.Should().BeTrue();
        message.Subject.Should().Be("Your order confirmation");
        message.Body.Should().Contain("Kitchen &amp; &lt;Friends&gt;")
            .And.Contain("Alex &lt;script&gt;")
            .And.Contain("BOX&lt;6&gt;")
            .And.Contain("DISH&amp;ONE")
            .And.Contain("Extra &lt;spice&gt;")
            .And.Contain("GBP 42.00")
            .And.Contain("2026-10-15 (Europe/London)")
            .And.Contain("1 &lt;Front&gt; Street")
            .And.Contain("Gate &lt;img src=x&gt;")
            .And.Contain("https://example.test/logo.png?variant=light&amp;size=2")
            .And.Contain("support&amp;orders@example.test")
            .And.NotContain("Spoofed brand")
            .And.NotContain("<script>")
            .And.NotContain("<img src=x>")
            .And.NotContain("{{");
        ((Dictionary<string, object?>)model["brand"]!)["display_name"].Should().Be("Spoofed brand",
            "rendering must not mutate the caller's model");
    }

    [Theory]
    [InlineData(TransactionalEmailTemplateNames.AccountSetupAccess, "Continue to your account")]
    [InlineData(TransactionalEmailTemplateNames.PasswordReset, "Reset password")]
    [InlineData(TransactionalEmailTemplateNames.EmailChangeConfirmation, "Confirm email address")]
    public async Task SendAsync_Should_RenderEscapedActionValues_WithNeutralFallbackBrand(string templateName, string action)
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var transport = new RecordingEmailSender();
        var sender = CreateSender(context, transport);
        var model = new Dictionary<string, object?>
        {
            ["first_name"] = "Alex <script>",
            ["action_url"] = "https://example.test/action?token=a&next=\"quoted\"",
            ["expires_at"] = "2026-10-15 12:00 UTC <test>",
            ["brand"] = new Dictionary<string, object?>
            {
                ["display_name"] = "Private administrative name",
                ["contact_email"] = "private@example.test"
            }
        };

        await sender.SendAsync(new(templateName, "buyer@example.test", model));

        var message = transport.Messages.Should().ContainSingle().Which;
        message.Body.Should().Contain("AONIK")
            .And.Contain(action)
            .And.Contain("Alex &lt;script&gt;")
            .And.Contain("href=\"https://example.test/action?token=a&amp;next=&quot;quoted&quot;\"")
            .And.Contain("2026-10-15 12:00 UTC &lt;test&gt;")
            .And.NotContain("private@example.test")
            .And.NotContain("Private administrative name")
            .And.NotContain("<img")
            .And.NotContain("Contact:")
            .And.NotContain("account has been created");
    }

    [Fact]
    public async Task SendAsync_Should_ReuseTenantOverrideAndBaseBinding()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var custom = new NotificationTemplate
        {
            TenantId = _tenantId, Name = "tenant.receipt", Channel = "Email",
            SubjectTemplate = "Our receipt", BodyTemplate = "<p>Local {{ purchaser_name | escape }}</p>"
        };
        var wrapper = new NotificationTemplate
        {
            TenantId = _tenantId, Name = "tenant.wrapper", Channel = "Email",
            BodyTemplate = "<article>{{ model.brand.display_name | escape }}{{ content }}</article>"
        };
        context.NotificationTemplates.AddRange(custom, wrapper);
        context.NotificationTemplateBindings.Add(new NotificationTemplateBinding
        {
            TenantId = _tenantId, TemplateName = TransactionalEmailTemplateNames.OrderConfirmation,
            Channel = "Email", OverrideTemplateId = custom.Id, BaseTemplateId = wrapper.Id
        });
        await context.SaveChangesAsync();
        var transport = new RecordingEmailSender();
        var sender = CreateSender(context, transport, new PublicBusinessProfileDto("Local Kitchen"));

        await sender.SendAsync(new(TransactionalEmailTemplateNames.OrderConfirmation, "buyer@example.test", ReceiptModel()));

        var message = transport.Messages.Should().ContainSingle().Which;
        message.Subject.Should().Be("Our receipt");
        message.Body.Should().Be("<article>Local Kitchen<p>Local Alex &lt;script&gt;</p></article>");
    }

    [Fact]
    public async Task SendAsync_Should_UseNamedTenantTemplateBeforeSharedDefault()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        context.NotificationTemplates.Add(new NotificationTemplate
        {
            TenantId = _tenantId, Name = TransactionalEmailTemplateNames.OrderConfirmation, Channel = "Email",
            SubjectTemplate = "Tenant receipt", BodyTemplate = "{{ order_id | escape }}"
        });
        await context.SaveChangesAsync();
        var transport = new RecordingEmailSender();

        await CreateSender(context, transport).SendAsync(
            new(TransactionalEmailTemplateNames.OrderConfirmation, "buyer@example.test", ReceiptModel()));

        transport.Messages.Should().ContainSingle().Which.Subject.Should().Be("Tenant receipt");
    }

    [Fact]
    public async Task SendAsync_Should_NotResolveAnotherTenantsBoundTemplate()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var other = new NotificationTemplate
        {
            TenantId = Guid.NewGuid(), Name = "other.receipt", Channel = "Email",
            SubjectTemplate = "Other tenant", BodyTemplate = "Private other tenant template"
        };
        context.NotificationTemplates.Add(other);
        context.NotificationTemplateBindings.Add(new NotificationTemplateBinding
        {
            TenantId = _tenantId, TemplateName = TransactionalEmailTemplateNames.OrderConfirmation,
            Channel = "Email", OverrideTemplateId = other.Id
        });
        await context.SaveChangesAsync();
        var transport = new RecordingEmailSender();
        var sender = CreateSender(context, transport);

        var act = () => sender.SendAsync(
            new(TransactionalEmailTemplateNames.OrderConfirmation, "buyer@example.test", ReceiptModel()));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
        transport.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_Should_NotSendWhenRenderingFails()
    {
        await using var context = CreateContext();
        context.NotificationTemplates.Add(new NotificationTemplate
        {
            TenantId = _tenantId, Name = TransactionalEmailTemplateNames.OrderConfirmation,
            Channel = "Email", BodyTemplate = "{% if %}"
        });
        await context.SaveChangesAsync();
        var transport = new RecordingEmailSender();
        var sender = CreateSender(context, transport);

        var act = () => sender.SendAsync(
            new(TransactionalEmailTemplateNames.OrderConfirmation, "buyer@example.test", ReceiptModel()));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Template parsing failed:*");
        transport.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_Should_PropagateProviderFailureAndCancellationToken()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var transport = new RecordingEmailSender { Failure = new InvalidOperationException("Provider unavailable") };
        var sender = CreateSender(context, transport);
        using var cancellation = new CancellationTokenSource();

        var act = () => sender.SendAsync(
            new(TransactionalEmailTemplateNames.OrderConfirmation, "buyer@example.test", ReceiptModel()), cancellation.Token);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Provider unavailable");
        transport.LastCancellationToken.Should().Be(cancellation.Token);
        transport.Messages.Should().BeEmpty();
    }

    private PlatformDbContext CreateContext()
        => new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"TemplatedEmail_{Guid.NewGuid()}").Options, new TestTenantProvider(_tenantId));

    private static Task SeedAsync(PlatformDbContext context)
        => new NotificationTemplateSeedService(context, NullLogger<NotificationTemplateSeedService>.Instance).SeedAsync();

    private TemplatedEmailSender CreateSender(PlatformDbContext context, IEmailSender transport,
        PublicBusinessProfileDto? profile = null)
    {
        var publicProfile = new Mock<IPublicBusinessProfileService>();
        publicProfile.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var tenant = new TestTenantProvider(_tenantId);
        return new TemplatedEmailSender(
            new NotificationTemplateService(context, tenant, new FluidNotificationTemplateRenderer()),
            transport, publicProfile.Object, tenant);
    }

    private static Dictionary<string, object?> ReceiptModel() => new()
    {
        ["order_id"] = "00000000-0000-0000-0000-000000000123",
        ["purchaser_name"] = "Alex <script>", ["currency"] = "GBP",
        ["subtotal"] = "40.00", ["discount_total"] = "0.00", ["tax_total"] = "0.00",
        ["delivery_total"] = "2.00", ["total"] = "42.00",
        ["items"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["description"] = "BOX<6>", ["quantity"] = 1, ["unit_price"] = "40.00", ["total"] = "40.00"
            }
        },
        ["selections"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["description"] = "DISH&ONE", ["quantity"] = 6, ["personalisation"] = "Extra <spice>"
            }
        },
        ["delivery"] = new Dictionary<string, object?>
        {
            ["date"] = "2026-10-15", ["timezone"] = "Europe/London", ["recipient_name"] = "Alex <script>",
            ["line1"] = "1 <Front> Street", ["line2"] = "", ["city"] = "London", ["region"] = "",
            ["postcode"] = "SW1A 1AA", ["country_code"] = "GB", ["notes"] = "Gate <img src=x>"
        }
    };

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public Exception? Failure { get; init; }
        public CancellationToken LastCancellationToken { get; private set; }
        public bool IsConfigured => false; // An options-only probe must not prevent runtime dispatch.
        public string ProviderName => "Test";
        public string? UnconfiguredReason => "No live sender in tests";

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            if (Failure is not null) throw Failure;
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
