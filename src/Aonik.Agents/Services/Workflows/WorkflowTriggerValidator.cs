using System.Text.RegularExpressions;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerValidator(IRecurrenceScheduleCalculator recurrence, IClock clock)
{
    public IReadOnlyList<WorkflowDiagnostic> Validate(WorkflowTriggerDefinition definition)
    {
        List<WorkflowDiagnostic> errors = [];
        if (definition is null || definition.SchemaVersion != 1 || definition.WorkflowVersionId == Guid.Empty || definition.AutomationIdentityId == Guid.Empty)
            return [new("InvalidTriggerContract", "definition", "Select a schema version, published workflow version and automation identity.")];
        if (definition.Source is not ("InvoiceCreated" or "InvoiceIssued" or "Schedule" or "GmailMessageAdded"))
            errors.Add(new("UnsupportedTriggerSource", "source", "Select an available typed trigger source."));
        WorkflowTriggerSubject subject = definition.Subject;
        bool documentSource = subject is { Type: "Document", Mapping: "MessageAttachments" } && definition.Source == "GmailMessageAdded";
        if (subject is null || (!documentSource && (subject.Type != "Invoice" || subject.Mapping is not ("EventSubject" or "Fixed"))) || subject.FixedIds is null
            || subject.FixedIds.Count > 100 || subject.FixedIds.Any(id => id == Guid.Empty) || subject.FixedIds.Distinct().Count() != subject.FixedIds.Count
            || (subject.Mapping is "EventSubject" or "MessageAttachments" ? subject.FixedIds.Count != 0 : subject.FixedIds.Count == 0)
            || (definition.Source is "Schedule" or "GmailMessageAdded" && !documentSource && subject.Mapping != "Fixed")
            || (definition.Source == "GmailMessageAdded" && !documentSource && subject.FixedIds.Count != 1))
            errors.Add(new("InvalidTriggerSubject", "subject", "Select event or fixed invoice IDs, or Document/MessageAttachments for Gmail with no fixed IDs."));
        WorkflowTriggerLimits limits = definition.Limits;
        if (limits is null || limits.MaximumStartsPerHour is < 1 or > 1000 || limits.MaximumConcurrentRuns is < 1 or > 10
            || limits.MaximumCausationDepth is < 1 or > 8)
            errors.Add(new("InvalidTriggerLimits", "limits", "Use 1–1000 starts/hour, 1–10 concurrent runs and a causation depth of 1–8."));
        if (definition.Source == "Schedule") ValidateSchedule(definition.Schedule, errors);
        else if (definition.Schedule is not null) errors.Add(new("UnexpectedSchedule", "schedule", "Only a schedule source may contain a recurrence."));
        if (definition.Source == "GmailMessageAdded") ValidateMailbox(definition.Mailbox, errors);
        else if (definition.Mailbox is not null) errors.Add(new("UnexpectedMailbox", "mailbox", "Only a Gmail source may contain mailbox synchronization settings."));
        if (!documentSource && definition.Mailbox?.SubjectPrefix is not null)
            errors.Add(new("UnexpectedMailboxSubjectPrefix", "mailbox.subjectPrefix", "Subject prefixes apply to Document attachment intake."));
        return errors;
    }

    private void ValidateSchedule(WorkflowTriggerSchedule? schedule, List<WorkflowDiagnostic> errors)
    {
        if (schedule is null) { errors.Add(new("ScheduleRequired", "schedule", "A recurring schedule is required.")); return; }
        bool validCron = schedule.Cron is { Length: <= 120 } && recurrence.IsValidCron(schedule.Cron);
        if (!validCron) errors.Add(new("InvalidCron", "schedule.cron", "Provide a valid bounded Quartz cron expression."));
        else if (!Regex.IsMatch(schedule.Cron.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0], "^[0-5]?[0-9]$", RegexOptions.NonBacktracking))
            errors.Add(new("ScheduleTooFrequent", "schedule.cron", "Choose one fixed second per minute; sub-minute admission schedules are unavailable."));
        bool validZone = false;
        if (schedule.TimeZone is { Length: > 0 and <= 100 })
        {
            try { TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone); validZone = schedule.TimeZone == "UTC" || zone.HasIanaId; }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        if (!validZone) errors.Add(new("InvalidTimezone", "schedule.timeZone", "Select a valid explicit IANA timezone; invalid values cannot fall back to UTC."));
        if (schedule.StartAtUtc.Kind != DateTimeKind.Utc || schedule.EndAtUtc.Kind != DateTimeKind.Utc || schedule.EndAtUtc <= schedule.StartAtUtc
            || schedule.EndAtUtc - schedule.StartAtUtc > TimeSpan.FromDays(366) || schedule.EndAtUtc <= clock.UtcNow)
            errors.Add(new("InvalidScheduleWindow", "schedule", "Provide a future-ending UTC schedule window of at most 366 days."));
        if (schedule.MaximumOccurrences is < 1 or > 10000 || schedule.MaximumCatchUpOccurrences is < 0 or > 10)
            errors.Add(new("InvalidOccurrenceLimit", "schedule", "Use 1–10000 occurrences and explicitly approve at most ten catch-up occurrences; zero skips missed times."));
        if (validCron && validZone)
        {
            DateTime? first = recurrence.GetNextOccurrenceUtc(schedule.Cron, schedule.TimeZone, schedule.StartAtUtc);
            DateTime? second = first.HasValue ? recurrence.GetNextOccurrenceUtc(schedule.Cron, schedule.TimeZone, first.Value) : null;
            if (first is null || first > schedule.EndAtUtc) errors.Add(new("EmptySchedule", "schedule", "The schedule has no occurrence in its approved window."));
            if (first.HasValue && second.HasValue && second - first < TimeSpan.FromMinutes(1))
                errors.Add(new("ScheduleTooFrequent", "schedule.cron", "Automatic admission schedules must be at least one minute apart."));
        }
    }

    private void ValidateMailbox(WorkflowTriggerMailbox? mailbox, List<WorkflowDiagnostic> errors)
    {
        if (mailbox is null) { errors.Add(new("MailboxRequired", "mailbox", "Pin a connection and workflow grant for Gmail synchronization.")); return; }
        if (mailbox.ConnectionId == Guid.Empty || mailbox.WorkflowGrantId == Guid.Empty || mailbox.Mode is not ("Polling" or "PushWithPolling")
            || mailbox.PollIntervalMinutes is < 5 or > 1440 || mailbox.LabelIds is null || mailbox.LabelIds.Count > 20
            || mailbox.LabelIds.Any(label => label is null || !Regex.IsMatch(label, "^[A-Za-z0-9_-]{1,128}$", RegexOptions.NonBacktracking))
            || mailbox.LabelIds.Distinct(StringComparer.Ordinal).Count() != mailbox.LabelIds.Count)
            errors.Add(new("InvalidMailboxSynchronization", "mailbox", "Pin an account/grant, polling or push-with-polling mode, 5–1440 minute polling and at most twenty unique label IDs."));
        if (mailbox.BackfillSinceUtc is { } since && (since.Kind != DateTimeKind.Utc || since > clock.UtcNow || since < clock.UtcNow.AddDays(-7)))
            errors.Add(new("InvalidMailboxBackfill", "mailbox.backfillSinceUtc", "An explicit backfill is bounded to the preceding seven days. Omit it to suppress historical automation."));
        if (mailbox.SubjectPrefix is not null && (string.IsNullOrWhiteSpace(mailbox.SubjectPrefix) || mailbox.SubjectPrefix.Length > 160
            || mailbox.SubjectPrefix.Any(char.IsControl)))
            errors.Add(new("InvalidMailboxSubjectPrefix", "mailbox.subjectPrefix", "Use an optional exact subject prefix of 1–160 printable characters."));
    }
}
