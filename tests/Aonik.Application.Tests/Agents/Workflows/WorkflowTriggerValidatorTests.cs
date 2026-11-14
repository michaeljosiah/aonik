using FluentAssertions;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.Platform.Services.Tasks;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Tests.Agents.Workflows;

public class WorkflowTriggerValidatorTests
{
    [Theory]
    [InlineData("Europe/London", true)]
    [InlineData("Africa/Lagos", true)]
    [InlineData("UTC", true)]
    [InlineData("Invalid/Zone", false)]
    [InlineData("GMT Standard Time", false)]
    [InlineData("", false)]
    public void Schedule_Should_RequireAnExplicitIanaTimezone(string timeZone, bool valid)
    {
        WorkflowTriggerDefinition definition = Scheduled();
        var diagnostics = Validator().Validate(definition with { Schedule = definition.Schedule! with { TimeZone = timeZone } });
        diagnostics.Any(value => value.Code == "InvalidTimezone").Should().Be(!valid);
    }

    [Theory]
    [InlineData("0 0/5 * * * ?", true)]
    [InlineData("0 0 9 * * ?", true)]
    [InlineData("0/5 * * * * ?", false)]
    [InlineData("0,45 * * * * ?", false)]
    [InlineData("invalid", false)]
    public void Schedule_Should_RejectSubMinuteOrInvalidRecurrence(string cron, bool valid)
    {
        WorkflowTriggerDefinition definition = Scheduled();
        var diagnostics = Validator().Validate(definition with { Schedule = definition.Schedule! with { Cron = cron } });
        if (valid) diagnostics.Should().BeEmpty();
        else diagnostics.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("2027-03-27T09:00:00Z", "2027-03-28T08:00:00Z")]
    [InlineData("2027-10-30T08:00:00Z", "2027-10-31T09:00:00Z")]
    public void Schedule_Should_PreserveLocalWallTimeAcrossDaylightSavingChanges(string after, string expected)
    {
        var recurrence = new RecurrenceCalculator();
        DateTime next = recurrence.GetNextOccurrenceUtc("0 0 9 * * ?", "Europe/London", DateTime.Parse(after).ToUniversalTime())!.Value;
        next.Should().Be(DateTime.Parse(expected).ToUniversalTime());
        recurrence.GetNextOccurrenceUtc("0 0 9 * * ?", "Europe/London", next).Should().BeAfter(next);
    }

    [Theory]
    [InlineData("window")]
    [InlineData("utc")]
    [InlineData("duration")]
    [InlineData("catchup")]
    [InlineData("occurrences")]
    [InlineData("concurrency")]
    [InlineData("hourly")]
    [InlineData("depth")]
    public void Schedule_Should_RejectUnboundedContracts(string change)
    {
        WorkflowTriggerDefinition definition = Scheduled();
        definition = change switch
        {
            "window" => definition with { Schedule = definition.Schedule! with { EndAtUtc = new Clock().UtcNow.AddDays(-1) } },
            "utc" => definition with { Schedule = definition.Schedule! with { StartAtUtc = DateTime.SpecifyKind(new Clock().UtcNow, DateTimeKind.Unspecified) } },
            "duration" => definition with { Schedule = definition.Schedule! with { EndAtUtc = new Clock().UtcNow.AddDays(367) } },
            "catchup" => definition with { Schedule = definition.Schedule! with { MaximumCatchUpOccurrences = 11 } },
            "occurrences" => definition with { Schedule = definition.Schedule! with { MaximumOccurrences = 10001 } },
            "concurrency" => definition with { Limits = definition.Limits with { MaximumConcurrentRuns = 11 } },
            "hourly" => definition with { Limits = definition.Limits with { MaximumStartsPerHour = 1001 } },
            _ => definition with { Limits = definition.Limits with { MaximumCausationDepth = 9 } },
        };
        Validator().Validate(definition).Should().NotBeEmpty();
    }

    [Fact]
    public void Mailbox_Should_RequireBoundedExplicitBackfillAndFixedSubject()
    {
        WorkflowTriggerDefinition definition = Scheduled() with { Source = "GmailMessageAdded", Schedule = null,
            Mailbox = new(Guid.NewGuid(), Guid.NewGuid(), ["INBOX"], "PushWithPolling", 15) };
        Validator().Validate(definition).Should().BeEmpty();
        Validator().Validate(definition with { Mailbox = definition.Mailbox! with { BackfillSinceUtc = new Clock().UtcNow.AddDays(-8) } }).Should().Contain(value => value.Code == "InvalidMailboxBackfill");
        Validator().Validate(definition with { Subject = new("Invoice", "EventSubject", []) }).Should().Contain(value => value.Code == "InvalidTriggerSubject");
        Validator().Validate(definition with { Mailbox = definition.Mailbox! with { PollIntervalMinutes = 1 } }).Should().NotBeEmpty();
    }

    [Fact]
    public void Mailbox_Should_AllowOnlyExplicitDocumentAttachmentMapping_WithBoundedSubjectPrefix()
    {
        var definition = Scheduled() with { Source = "GmailMessageAdded", Schedule = null, Subject = new("Document", "MessageAttachments", []),
            Mailbox = new(Guid.NewGuid(), Guid.NewGuid(), ["INBOX"], "Polling", 15, SubjectPrefix: "Qualification:") };
        Validator().Validate(definition).Should().BeEmpty();
        Validator().Validate(definition with { Subject = new("Document", "Fixed", [Guid.NewGuid()]) }).Should().Contain(value => value.Code == "InvalidTriggerSubject");
        Validator().Validate(definition with { Subject = definition.Subject with { FixedIds = [Guid.NewGuid()] } }).Should().Contain(value => value.Code == "InvalidTriggerSubject");
        Validator().Validate(definition with { Mailbox = definition.Mailbox with { SubjectPrefix = "injected\nheader" } }).Should().Contain(value => value.Code == "InvalidMailboxSubjectPrefix");
        Validator().Validate(definition with { Subject = new("Invoice", "Fixed", [Guid.NewGuid()]) }).Should().Contain(value => value.Code == "UnexpectedMailboxSubjectPrefix");
    }

    private static WorkflowTriggerValidator Validator() => new(new RecurrenceCalculator(), new Clock());
    private static WorkflowTriggerDefinition Scheduled() => new(1, "Schedule", Guid.NewGuid(), Guid.NewGuid(), new("Invoice", "Fixed", [Guid.NewGuid()]), new(),
        new("0 0 9 * * ?", "Europe/London", new Clock().UtcNow, new Clock().UtcNow.AddDays(30), 30));
    private sealed class Clock : IClock { public DateTime UtcNow => new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
}
