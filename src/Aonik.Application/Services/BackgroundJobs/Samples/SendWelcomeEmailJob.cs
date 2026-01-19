using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Aonik.Application.Abstractions.BackgroundJobs;

namespace Aonik.Application.Services.BackgroundJobs.Samples;

/// <summary>
/// Sample background job that sends a welcome email.
/// This demonstrates how to implement an async background job.
/// </summary>
public class SendWelcomeEmailJob : IAsyncBackgroundJob<SendWelcomeEmailArgs>
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SendWelcomeEmailJob> _logger;

    public SendWelcomeEmailJob(IEmailSender emailSender, ILogger<SendWelcomeEmailJob> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task ExecuteAsync(SendWelcomeEmailArgs args, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending welcome email to {Email} for tenant {TenantId}",
            args.Email,
            args.TenantId);

        // Simulate email sending
        await _emailSender.SendEmailAsync(
            args.Email,
            "Welcome to AONIK!",
            $"Hello {args.FirstName},\n\nWelcome to the AONIK platform! We're excited to have you on board.\n\nBest regards,\nThe AONIK Team",
            cancellationToken);

        _logger.LogInformation(
            "Successfully sent welcome email to {Email}",
            args.Email);
    }
}

/// <summary>
/// Arguments for the <see cref="SendWelcomeEmailJob"/>.
/// </summary>
public class SendWelcomeEmailArgs
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
}

/// <summary>
/// Simple email sender interface for demonstration purposes.
/// In production, this would be replaced with the actual email service.
/// </summary>
public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
