using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Seeding;
using Aonik.Platform.Services.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Notifications;

public sealed class NotificationTemplateSeedServiceTests
{
    private static readonly string[] TransactionalNames =
    [
        TransactionalEmailTemplateNames.OrderConfirmation,
        TransactionalEmailTemplateNames.GiftCardDelivery,
        TransactionalEmailTemplateNames.AccountSetupAccess,
        TransactionalEmailTemplateNames.PasswordReset,
        TransactionalEmailTemplateNames.EmailChangeConfirmation,
        ContactEnquiryEmailTemplates.Staff,
        ContactEnquiryEmailTemplates.Acknowledgement
    ];

    [Fact]
    public async Task SeedAsync_Should_AddMissingSharedTransactionalTemplates_OnlyOnce()
    {
        await using var context = CreateContext(Guid.NewGuid());
        var service = new NotificationTemplateSeedService(context, NullLogger<NotificationTemplateSeedService>.Instance);

        await service.SeedAsync();
        var firstIds = await context.NotificationTemplates.Select(x => x.Id).ToArrayAsync();
        await service.SeedAsync();

        (await context.NotificationTemplates.Select(x => x.Id).ToArrayAsync()).Should().BeEquivalentTo(firstIds);
        var templates = await context.NotificationTemplates.Where(x => TransactionalNames.Contains(x.Name)).ToListAsync();
        templates.Should().HaveCount(7);
        templates.Should().OnlyContain(x => x.TenantId == null && x.IsShared && x.IsActive && x.Channel == "Email");
        templates.Select(x => x.Name).Should().BeEquivalentTo(TransactionalNames);
        templates.Should().OnlyContain(x => x.BodyTemplate.Contains("brand.display_name | escape")
            && x.BodyTemplate.Contains("<header>") && x.BodyTemplate.Contains("<footer>"));
    }

    [Fact]
    public async Task SeedAsync_Should_PreserveAuthoredSharedTemplatesAndTenantOverrides()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var shared = new NotificationTemplate
        {
            Name = TransactionalEmailTemplateNames.OrderConfirmation, Channel = "Email", IsShared = true,
            IsActive = false, SubjectTemplate = "Authored shared subject", BodyTemplate = "Authored shared body"
        };
        var custom = new NotificationTemplate
        {
            TenantId = tenantId, Name = TransactionalEmailTemplateNames.OrderConfirmation, Channel = "Email",
            SubjectTemplate = "Authored tenant subject", BodyTemplate = "Authored tenant body"
        };
        context.NotificationTemplates.AddRange(shared, custom);
        var binding = new NotificationTemplateBinding
        {
            TenantId = tenantId, TemplateName = TransactionalEmailTemplateNames.OrderConfirmation,
            Channel = "Email", OverrideTemplateId = custom.Id
        };
        context.NotificationTemplateBindings.Add(binding);
        await context.SaveChangesAsync();
        var service = new NotificationTemplateSeedService(context, NullLogger<NotificationTemplateSeedService>.Instance);

        await service.SeedAsync();
        await service.SeedAsync();
        context.ChangeTracker.Clear();

        var savedShared = await context.NotificationTemplates.SingleAsync(x => x.Id == shared.Id);
        savedShared.SubjectTemplate.Should().Be("Authored shared subject");
        savedShared.BodyTemplate.Should().Be("Authored shared body");
        savedShared.IsActive.Should().BeFalse();
        var savedTenant = await context.NotificationTemplates.SingleAsync(x => x.Id == custom.Id);
        savedTenant.SubjectTemplate.Should().Be("Authored tenant subject");
        savedTenant.BodyTemplate.Should().Be("Authored tenant body");
        (await context.NotificationTemplateBindings.SingleAsync(x => x.Id == binding.Id))
            .OverrideTemplateId.Should().Be(custom.Id);
        (await context.NotificationTemplates.CountAsync(x => x.TenantId == null
            && TransactionalNames.Contains(x.Name))).Should().Be(7);
    }

    private static PlatformDbContext CreateContext(Guid tenantId)
        => new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"TemplateSeeds_{Guid.NewGuid()}").Options, new TestTenantProvider(tenantId));
}
