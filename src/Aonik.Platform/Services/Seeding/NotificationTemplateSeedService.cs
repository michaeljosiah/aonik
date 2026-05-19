using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Seeding;

/// <summary>
/// Seeds shared (tenant-agnostic) notification templates for the registration flow.
/// Only inserts templates that don't already exist (matched by Name + Channel).
/// Idempotent and safe to call on every startup.
/// Tenants can override these by creating a <see cref="NotificationTemplateBinding"/>
/// that points to their own template via OverrideTemplateId.
/// </summary>
internal class NotificationTemplateSeedService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<NotificationTemplateSeedService> _logger;

    public NotificationTemplateSeedService(PlatformDbContext dbContext, ILogger<NotificationTemplateSeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting notification template seed process...");

        var defaults = GetDefaultTemplates();

        // Bypass tenant query filter — shared templates have TenantId = null
        var existingKeys = await _dbContext.NotificationTemplates
            .AcrossTenants()
            .Where(t => t.TenantId == null && t.IsShared)
            .Select(t => new { t.Name, t.Channel })
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(
            existingKeys.Select(k => $"{k.Name}|{k.Channel}"),
            StringComparer.OrdinalIgnoreCase);

        var newTemplates = defaults
            .Where(t => !existingSet.Contains($"{t.Name}|{t.Channel}"))
            .ToList();

        if (newTemplates.Count > 0)
        {
            await _dbContext.NotificationTemplates.AddRangeAsync(newTemplates, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded {Count} notification templates", newTemplates.Count);
        }
        else
        {
            _logger.LogInformation("All notification templates already exist — skipping seed");
        }
    }

    private static List<NotificationTemplate> GetDefaultTemplates() =>
    [
        new NotificationTemplate
        {
            Name = NotificationTemplateNames.WelcomeEmail,
            Channel = "Email",
            IsShared = true,
            IsActive = true,
            Description = "Sent to new users after successful registration",
            SubjectTemplate = "Welcome to {{ tenant_name }}!",
            BodyTemplate = """
                <h1>Welcome, {{ first_name }}!</h1>
                <p>Thank you for joining <strong>{{ tenant_name }}</strong>. Your account has been created successfully.</p>
                <p>Here's what you can do next:</p>
                <ul>
                    <li>Complete your profile</li>
                    <li>Explore available services</li>
                    <li>Link your financial accounts</li>
                </ul>
                <p>If you have any questions, our support team is here to help.</p>
                <p>Best regards,<br/>The {{ tenant_name }} Team</p>
                """
        },
        new NotificationTemplate
        {
            Name = NotificationTemplateNames.EmailConfirmation,
            Channel = "Email",
            IsShared = true,
            IsActive = true,
            Description = "Sent to verify the user's email address via a confirmation link",
            SubjectTemplate = "Confirm your email address",
            BodyTemplate = """
                <h1>Confirm your email</h1>
                <p>Hi {{ first_name }},</p>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href="{{ confirmation_url }}">Confirm Email Address</a></p>
                <p>This link will expire in {{ expiry_hours }} hours.</p>
                <p>If you did not create an account, you can safely ignore this email.</p>
                <p>Best regards,<br/>The {{ tenant_name }} Team</p>
                """
        },
        new NotificationTemplate
        {
            Name = NotificationTemplateNames.EmailOtp,
            Channel = "Email",
            IsShared = true,
            IsActive = true,
            Description = "Sent to deliver a one-time password via email",
            SubjectTemplate = "Your verification code: {{ otp_code }}",
            BodyTemplate = """
                <h1>Your verification code</h1>
                <p>Hi {{ first_name }},</p>
                <p>Your one-time verification code is:</p>
                <p style="font-size: 32px; font-weight: bold; letter-spacing: 8px; text-align: center; padding: 16px;">{{ otp_code }}</p>
                <p>This code will expire in {{ expiry_minutes }} minutes.</p>
                <p>If you did not request this code, please ignore this email or contact support.</p>
                <p>Best regards,<br/>The {{ tenant_name }} Team</p>
                """
        },
        new NotificationTemplate
        {
            Name = NotificationTemplateNames.SmsOtp,
            Channel = "SMS",
            IsShared = true,
            IsActive = true,
            Description = "Sent to deliver a one-time password via SMS",
            SubjectTemplate = "",
            BodyTemplate = "{{ tenant_name }}: Your verification code is {{ otp_code }}. It expires in {{ expiry_minutes }} minutes. Do not share this code."
        },
        new NotificationTemplate
        {
            Name = NotificationTemplateNames.AdminUserInvitation,
            Channel = "Email",
            IsShared = true,
            IsActive = true,
            Description = "Sent to an invited user with a tenant-scoped sign-in link",
            SubjectTemplate = "You've been invited to join {{ tenant_name }}",
            BodyTemplate = """
                <h1>You're invited to join {{ tenant_name }}</h1>
                <p>Hi {{ invitee_display_name }},</p>
                <p><strong>{{ operator_display_name }}</strong> has invited you to access <strong>{{ tenant_name }}</strong>{{ roles_granted_suffix }}.</p>
                <p>Click the link below to accept the invitation and sign in:</p>
                <p><a href="{{ invite_url }}">Accept invitation</a></p>
                <p>The invitation expires on <strong>{{ expiry_utc }}</strong>. If you did not expect this invitation, you can safely ignore this email.</p>
                <p>Best regards,<br/>The {{ tenant_name }} Team</p>
                """
        }
    ];
}
