using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.SharedKernel.Abstractions.Connectors;

/// <summary>The code-owned minimum classifications shared by catalog, agent gate and durable handlers.</summary>
public static class GmailActionPolicies
{
    public static readonly IReadOnlyDictionary<string, string> ToolNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [GmailActionKeys.GetDraft] = "gmail_get_draft", [GmailActionKeys.PrepareMessage] = "gmail_prepare_message",
        [GmailActionKeys.CreateDraft] = "gmail_create_draft", [GmailActionKeys.UpdateDraft] = "gmail_update_draft", [GmailActionKeys.DeleteDraft] = "gmail_delete_draft",
        [GmailActionKeys.SendMessage] = "gmail_send_message", [GmailActionKeys.ReplyMessage] = "gmail_reply_message", [GmailActionKeys.ForwardMessage] = "gmail_forward_message", [GmailActionKeys.SendDraft] = "gmail_send_draft",
        [GmailActionKeys.ModifyLabels] = "gmail_modify_labels", [GmailActionKeys.MarkRead] = "gmail_mark_read", [GmailActionKeys.MarkUnread] = "gmail_mark_unread",
        [GmailActionKeys.Archive] = "gmail_archive_messages", [GmailActionKeys.Trash] = "gmail_trash_messages", [GmailActionKeys.Restore] = "gmail_restore_messages", [GmailActionKeys.PermanentDelete] = "gmail_permanently_delete_messages",
    };
    public static ToolApprovalOptions? Approval(string key) => key switch
    {
        GmailActionKeys.GetDraft => null,
        GmailActionKeys.PrepareMessage => new(ToolApprovalTier.Low, "Prepare immutable Gmail message"),
        GmailActionKeys.SendMessage or GmailActionKeys.ReplyMessage or GmailActionKeys.ForwardMessage or GmailActionKeys.SendDraft or GmailActionKeys.PermanentDelete
            => new(ToolApprovalTier.High, ToolNames[key], "Integrations." + key),
        _ when ToolNames.ContainsKey(key) => new(ToolApprovalTier.Medium, ToolNames[key]),
        _ => throw new ArgumentException("Unknown Gmail mutation contract."),
    };
    public static string? ActionForTool(string toolName) => ToolNames.SingleOrDefault(pair => pair.Value == toolName).Key;
    public static string? ActionForProposal(string proposalType) => ToolNames.Keys.SingleOrDefault(key => Approval(key)?.ProposalType == proposalType);
    public static bool IsMutation(string key) => ToolNames.ContainsKey(key) && Approval(key)?.Tier is ToolApprovalTier.Medium or ToolApprovalTier.High;
}
