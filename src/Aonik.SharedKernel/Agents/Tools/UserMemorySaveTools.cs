using System.ComponentModel;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.SharedKernel.Agents.Tools;

/// <summary>
/// Cross-cutting AITool that saves user memory entries mid-conversation.
/// Lives on SharedKernel so any domain agent can surface this write-back
/// memory channel without taking a back-pointing reference on the Agents
/// runtime.
/// </summary>
/// <remarks>
/// No approval gate — the agent saves directly when it identifies something
/// worth remembering. The tool call is visible in the AG-UI chat stream,
/// so the user always sees what was saved and can correct it.
/// <para>
/// Typical triggers: user states a preference, shares a personal fact,
/// corrects a previous assumption, or provides identity information.
/// </para>
/// </remarks>
public sealed class UserMemorySaveTools
{
    private readonly IUserMemorySaveProvider _saveProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    private UserMemorySaveTools(
        IUserMemorySaveProvider saveProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _saveProvider = saveProvider;
        _currentUserProvider = currentUserProvider;
    }

    [Description(
        "Save a piece of information about the user to long-term memory. " +
        "Use when the user states a preference, shares a personal fact, provides identity " +
        "information, or corrects something you previously assumed. " +
        "Examples: 'I get paid on the 15th', 'I prefer to pay bills early', " +
        "'My household has 4 people', 'Actually I moved to London last month'. " +
        "Do NOT save transient conversation details, greetings, or information " +
        "that already exists in domain entities (accounts, transactions, bills).")]
    public async Task<UserMemorySaveToolResult> SaveUserMemory(
        [Description("Namespaced key using dot notation " +
            "(e.g. 'finance.preferred_pay_day', 'identity.household_size', " +
            "'preference.communication_style', 'identity.location'). " +
            "Use consistent keys — saving to an existing key supersedes the previous value.")]
        string key,
        [Description("The value to remember, as a JSON string. " +
            "For simple values use a quoted string (e.g. '\"15th\"'). " +
            "For structured data use a JSON object (e.g. '{\"city\": \"London\", \"country\": \"UK\"}').")]
        string valueJson,
        [Description("Category of memory: 'Identity' (personal facts like location, household), " +
            "'Preference' (stated preferences like pay timing, communication style), " +
            "'Correction' (user correcting a previous assumption), " +
            "'Fact' (general learned facts like income patterns, spending habits).")]
        string entryType,
        [Description("How confident are you in this information? " +
            "1.0 = user explicitly stated it, 0.8 = clearly implied, 0.6 = reasonably inferred. " +
            "Use 1.0 when quoting the user directly.")]
        decimal confidence = 1.0m,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
            return new UserMemorySaveToolResult(false, key, "Unable to identify current user.");

        // Determine source based on confidence — explicit statements = UserStated,
        // inferences = AiInferred.
        var source = confidence >= 0.9m ? "UserStated" : "AiInferred";

        var request = new UserMemorySaveRequest(
            UserId: userId,
            EntryType: entryType,
            Key: key,
            ValueJson: valueJson,
            Confidence: Math.Clamp(confidence, 0.1m, 1.0m),
            Source: source);

        var result = await _saveProvider.SaveAsync(request, cancellationToken);

        return new UserMemorySaveToolResult(
            Saved: true,
            Key: result.Key,
            Message: result.WasSuperseded
                ? $"Updated memory for '{result.Key}' (previous value superseded)."
                : $"Saved new memory for '{result.Key}'.");
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var saveProvider = serviceProvider.GetService<IUserMemorySaveProvider>();
        var currentUserProvider = serviceProvider.GetService<ICurrentUserProvider>();

        // If either dependency is missing, skip — tool won't be available
        if (saveProvider is null || currentUserProvider is null)
            yield break;

        var tools = new UserMemorySaveTools(saveProvider, currentUserProvider);

        yield return AIFunctionFactory.Create(
            tools.SaveUserMemory,
            name: "user_memory_save");
    }
}

/// <summary>
/// Result returned to the agent (and visible in the chat stream) after a memory save.
/// </summary>
public record UserMemorySaveToolResult(
    bool Saved,
    string Key,
    string Message);
