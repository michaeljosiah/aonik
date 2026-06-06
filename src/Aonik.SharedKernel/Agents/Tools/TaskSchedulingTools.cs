using System.ComponentModel;
using System.Text.Json;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.SharedKernel.Agents.Tools;

/// <summary>
/// Cross-cutting AITools that let the chat agent schedule reminders and tasks for the
/// current user (Spec 034). Lives on SharedKernel — alongside the memory and document-search
/// tools — so the master orchestrator can surface scheduling directly without taking a
/// reference on any domain module. Reminders are routed through <see cref="ITaskService"/>
/// (the platform dispatcher fires them) and always target the <em>current</em> user — the
/// agent cannot notify arbitrary people. The tools only create <c>notify_user</c> reminders
/// (reversible, no money); they are classified Low so the approval gate runs them in-band
/// with an audit record. Any high-risk action a scheduled task later fires is still gated
/// into a Proposal by the dispatcher at run time.
/// </summary>
public sealed class TaskSchedulingTools
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ITaskService _taskService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IClock _clock;

    private TaskSchedulingTools(
        ITaskService taskService,
        ICurrentUserProvider currentUserProvider,
        IClock clock)
    {
        _taskService = taskService;
        _currentUserProvider = currentUserProvider;
        _clock = clock;
    }

    [Description("Schedule a reminder for the current user. The user gets an in-app notification when it is due. " +
        "Provide exactly one timing: inMinutes (fire this many minutes from now), runAtUtc (a one-off at a specific UTC time), " +
        "or recurrenceCron (a repeating reminder as a Quartz 6-field cron with seconds, e.g. '0 0 9 * * ?' = 9am daily). " +
        "If you give none, it fires on the next dispatch sweep (within a minute).")]
    public async Task<object> ScheduleReminder(
        [Description("Short title of the reminder, e.g. 'Call mum'.")] string title,
        [Description("The reminder message shown to the user. If omitted, the title is reused.")] string? body = null,
        [Description("Fire this many minutes from now (use for 'in 30 minutes', 'in 2 hours').")] int? inMinutes = null,
        [Description("One-off fire time as a UTC ISO-8601 timestamp. Use only if you know the absolute UTC time.")] DateTime? runAtUtc = null,
        [Description("Quartz cron (6-field, with seconds) for a repeating reminder, e.g. '0 0 9 * * ?' for 9am daily.")] string? recurrenceCron = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new { error = "A reminder title is required." };
        }

        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            return new { error = "No signed-in user to remind." };
        }

        var message = string.IsNullOrWhiteSpace(body) ? title : body;
        DateTime? resolvedRunAt = inMinutes is { } mins ? _clock.UtcNow.AddMinutes(mins) : runAtUtc;

        var payloadJson = JsonSerializer.Serialize(
            new { userId, title, body = message, severity = "Info" },
            PayloadOptions);

        try
        {
            var result = await _taskService.ScheduleAsync(
                new ScheduleTaskRequest(
                    Title: title,
                    Kind: TaskKinds.Reminder,
                    ActionType: TaskActionTypes.NotifyUser,
                    ActionPayloadJson: payloadJson,
                    AssigneeType: TaskAssigneeTypes.User,
                    AssigneeId: userId,
                    RunAtUtc: resolvedRunAt,
                    RecurrenceCron: recurrenceCron,
                    SourceModule: "Agent"),
                cancellationToken).ConfigureAwait(false);

            return new
            {
                reminderId = result.Id,
                result.Title,
                result.Status,
                result.ScheduleType,
                result.NextRunAtUtc,
            };
        }
        catch (ArgumentException ex)
        {
            // e.g. an invalid cron — surface a plain message the agent can relay.
            return new { error = ex.Message };
        }
    }

    [Description("List the reminders and scheduled tasks the current user has set, with their status and next run time. " +
        "Use the returned reminderId with cancel_reminder.")]
    public async Task<IReadOnlyList<object>> ListReminders(CancellationToken cancellationToken = default)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            return [];
        }

        var tasks = await _taskService
            .ListForAssigneeAsync(TaskAssigneeTypes.User, userId, cancellationToken)
            .ConfigureAwait(false);

        return tasks
            .Select(t => (object)new
            {
                reminderId = t.Id,
                t.Title,
                t.Status,
                t.ScheduleType,
                t.NextRunAtUtc,
                t.RecurrenceCron,
            })
            .ToList();
    }

    [Description("Cancel one of the current user's reminders/scheduled tasks by its reminderId (from list_reminders). It will not fire again.")]
    public async Task<object> CancelReminder(
        [Description("The reminderId (GUID) returned by create_reminder or list_reminders.")] Guid reminderId,
        CancellationToken cancellationToken = default)
    {
        // Confirm the reminder belongs to the current user before cancelling, so the agent
        // can't cancel another user's task by guessing an id.
        var existing = await _taskService.GetAsync(reminderId, cancellationToken).ConfigureAwait(false);
        if (existing is null
            || existing.AssigneeType != TaskAssigneeTypes.User
            || !_currentUserProvider.TryGetCurrentUserId(out var userId)
            || existing.AssigneeId != userId)
        {
            return new { error = "Reminder not found." };
        }

        await _taskService.CancelAsync(reminderId, cancellationToken).ConfigureAwait(false);
        return new { reminderId, status = TaskStatuses.Cancelled };
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var taskService = serviceProvider.GetService<ITaskService>();
        var currentUserProvider = serviceProvider.GetService<ICurrentUserProvider>();
        var clock = serviceProvider.GetService<IClock>();

        // If any dependency is missing (e.g. a host without the Platform module), skip —
        // the reminder tools simply won't be offered to the agent.
        if (taskService is null || currentUserProvider is null || clock is null)
        {
            yield break;
        }

        var tools = new TaskSchedulingTools(taskService, currentUserProvider, clock);

        // Tool names use a recognised mutation verb (create_/cancel_) so they trip the gate's
        // fail-closed name heuristic, keeping it in step with the manifest classification.
        yield return AIFunctionFactory.Create(tools.ScheduleReminder, name: "create_reminder");
        yield return AIFunctionFactory.Create(tools.ListReminders, name: "list_reminders");
        yield return AIFunctionFactory.Create(tools.CancelReminder, name: "cancel_reminder");
    }
}
