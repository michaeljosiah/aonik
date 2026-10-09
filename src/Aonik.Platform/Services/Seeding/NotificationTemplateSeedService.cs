using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Seeding;

/// <summary>
/// Seeds shared (tenant-agnostic) registration and transactional notification templates.
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
        },
        TransactionalTemplate(
            TransactionalEmailTemplateNames.OrderConfirmation,
            "Sent after a recorded Commerce payment and order have completed",
            "Your order confirmation",
            """
            <h1>Your order is confirmed</h1>
            <p>Hi {{ purchaser_name | escape }},</p>
            <p>We have received your payment for order <strong>{{ order_id | escape }}</strong>.</p>
            <h2>Your order</h2>
            <table>
              <thead><tr><th>Item</th><th>Quantity</th><th>Unit price</th><th>Total</th></tr></thead>
              <tbody>
              {% for item in items %}
                <tr><td>{{ item.description | escape }}</td><td>{{ item.quantity | escape }}</td><td>{{ currency | escape }} {{ item.unit_price | escape }}</td><td>{{ currency | escape }} {{ item.total | escape }}</td></tr>
              {% endfor %}
              </tbody>
            </table>
            {% if selections != empty %}
            <h3>Box selections</h3>
            <ul>{% for selection in selections %}<li>{{ selection.description | escape }} &times; {{ selection.quantity | escape }}{% if selection.personalisation != blank %} ({{ selection.personalisation | escape }}){% endif %}</li>{% endfor %}</ul>
            {% endif %}
            <p>Subtotal: {{ currency | escape }} {{ subtotal | escape }}<br/>
            Discount: {{ currency | escape }} {{ discount_total | escape }}<br/>
            Tax: {{ currency | escape }} {{ tax_total | escape }}<br/>
            Delivery: {{ currency | escape }} {{ delivery_total | escape }}<br/>
            <strong>Total paid: {{ currency | escape }} {{ total | escape }}</strong></p>
            {% if delivery %}
            <h2>Delivery details</h2>
            <p>Delivery date: {{ delivery.date | escape }} ({{ delivery.timezone | escape }})</p>
            <p>{{ delivery.recipient_name | escape }}<br/>
            {{ delivery.line1 | escape }}<br/>
            {% if delivery.line2 != blank %}{{ delivery.line2 | escape }}<br/>{% endif %}
            {{ delivery.city | escape }}<br/>
            {% if delivery.region != blank %}{{ delivery.region | escape }}<br/>{% endif %}
            {{ delivery.postcode | escape }}<br/>
            {{ delivery.country_code | escape }}</p>
            {% if delivery.notes != blank %}<p>Delivery notes: {{ delivery.notes | escape }}</p>{% endif %}
            {% endif %}
            """),
        TransactionalTemplate(
            TransactionalEmailTemplateNames.AccountSetupAccess,
            "Ready for secure account setup or access links issued by the account flow",
            "Set up or access your account",
            """
            <h1>Set up or access your account</h1>
            <p>Hi {{ first_name | escape }},</p>
            <p>Use the secure link below to continue setting up or accessing your account.</p>
            <p><a href="{{ action_url | escape }}">Continue to your account</a></p>
            <p>This link expires at {{ expires_at | escape }}.</p>
            <p>If you did not request this email, you can ignore it.</p>
            """),
        TransactionalTemplate(
            TransactionalEmailTemplateNames.PasswordReset,
            "Ready for secure password reset links issued by the account flow",
            "Reset your password",
            """
            <h1>Reset your password</h1>
            <p>Hi {{ first_name | escape }},</p>
            <p>Use the secure link below to choose a new password.</p>
            <p><a href="{{ action_url | escape }}">Reset password</a></p>
            <p>This link expires at {{ expires_at | escape }}.</p>
            <p>If you did not request a password reset, you can ignore this email.</p>
            """),
        TransactionalTemplate(
            TransactionalEmailTemplateNames.EmailChangeConfirmation,
            "Ready for verification links sent to a proposed new account email address",
            "Confirm your new email address",
            """
            <h1>Confirm your new email address</h1>
            <p>Hi {{ first_name | escape }},</p>
            <p>Confirm this email address to continue your requested account email change.</p>
            <p><a href="{{ action_url | escape }}">Confirm email address</a></p>
            <p>This link expires at {{ expires_at | escape }}.</p>
            <p>If you did not request this change, you can ignore this email.</p>
            """),
        TransactionalTemplate(
            ContactEnquiryEmailTemplates.Staff,
            "Private staff notification for a durably accepted contact enquiry",
            "New contact enquiry",
            """
            <h1>New contact enquiry</h1>
            <p>Reference: {{ enquiry_id | escape }}<br/>Received: {{ received_at | escape }}</p>
            <p>Name: {{ name | escape }}<br/>Email: {{ email | escape }}<br/>Topic: {{ topic | escape }}</p>
            {% if order_number != blank %}<p>Unverified order reference: {{ order_number | escape }}</p>{% endif %}
            <p style="white-space: pre-wrap;">{{ message | escape }}</p>
            <p><a href="{{ detail_url | escape }}">View enquiry and private images</a></p>
            """),
        TransactionalTemplate(
            ContactEnquiryEmailTemplates.Acknowledgement,
            "Reference-only acknowledgement of a durably accepted contact enquiry",
            "We received your enquiry",
            """
            <h1>We received your enquiry</h1>
            <p>Your enquiry was received at {{ received_at | escape }}.</p>
            <p>Your reference is <strong>{{ enquiry_id | escape }}</strong>.</p>
            <p>If you did not submit an enquiry, you can ignore this email.</p>
            """)
    ];

    private static NotificationTemplate TransactionalTemplate(string name, string description, string subject, string body)
        => new()
        {
            Name = name,
            Channel = "Email",
            IsShared = true,
            IsActive = true,
            Description = description,
            SubjectTemplate = subject,
            BodyTemplate = """
                <div style="font-family: sans-serif; max-width: 640px; margin: auto;">
                <header>
                  {% if brand.logo_url != blank %}<img src="{{ brand.logo_url | escape }}" alt="{{ brand.display_name | escape }}" style="max-width: 200px;"/>{% endif %}
                  <p><strong>{{ brand.display_name | escape }}</strong></p>
                </header>
                <main>
                """ + body + """
                </main>
                <footer>
                  <p>{{ brand.display_name | escape }}</p>
                  {% if brand.contact_email != blank %}<p>Contact: {{ brand.contact_email | escape }}</p>{% endif %}
                  {% if brand.contact_phone != blank %}<p>{{ brand.contact_phone | escape }}</p>{% endif %}
                  {% if brand.website != blank %}<p><a href="{{ brand.website | escape }}">{{ brand.website | escape }}</a></p>{% endif %}
                </footer>
                </div>
                """
        };
}
