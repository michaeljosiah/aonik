using System.Security.Cryptography;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Platform.Services.Integrations;

internal sealed class FinancialDocumentMailboxReader(IWorkflowMailboxBindingReader bindings, ConnectorService connectors,
    ConnectorAuthorization authorization, ConnectorCatalog catalog, IConnectorContentStore content, IClock clock)
    : IFinancialDocumentMailboxReader
{
    private const int MaximumBytes = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<FinancialDocumentAttachment>> ReadAsync(Guid bindingId, int revision, string messageId,
        CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(60));
        CancellationToken token = budget.Token;
        ConnectorSubscriptionBinding binding = await bindings.RequireActiveAsync(bindingId, revision, token);
        if (binding.SubjectType != "Document") throw new PermissionDeniedException("Document.Extract");
        var actor = new SharedKernel.Abstractions.Workflows.WorkflowActor("AutomationIdentity", binding.AutomationIdentityId,
            WorkflowVersionId: binding.WorkflowVersionId);
        var context = new ConnectorUseContext(actor, binding.WorkflowId, binding.WorkflowVersionId,
            PinnedGrantId: binding.Connection.WorkflowGrantId, AllowedActions: binding.Connection.AllowedActions);
        var connection = await authorization.RequireUseAsync(binding.Connection.ConnectionId, catalog.Resolve(new(GmailActionKeys.GetMessage, 1)), context, token);
        string accountId = connection.ProviderAccountId;
        GmailSourceMessageResult envelope = ConnectorJson.Read<GmailSourceMessageResult>(await connectors.InvokeSourceReadAsync(binding,
            new(GmailActionKeys.GetMessage, 1), ConnectorJson.Element(new GmailGetMessageRequest(messageId)), token));
        GmailMessageResult message = envelope.Message;
        if (message.Id != messageId || message.Truncated) throw new ConnectorProviderException("SourceMessageInvalid");
        if (envelope.ReceivedAtUtc.Kind != DateTimeKind.Utc || envelope.ReceivedAtUtc > clock.UtcNow.AddMinutes(5)
            || envelope.ReceivedAtUtc < (binding.BackfillSinceUtc ?? binding.ActivatedAtUtc ?? clock.UtcNow)
            || binding.LabelIds.Any(label => !envelope.LabelIds.Contains(label, StringComparer.Ordinal))
            || (binding.IgnoreSelfGenerated && envelope.LabelIds.Contains("SENT", StringComparer.Ordinal))
            || (binding.SubjectPrefix is { } prefix && !message.Headers.GetValueOrDefault("Subject", "").StartsWith(prefix, StringComparison.Ordinal))) return [];
        GmailAttachmentMetadata[] eligible = message.Attachments.Where(attachment => Supported(attachment.ContentType)).ToArray();
        if (eligible.Length > 5 || eligible.Any(attachment => attachment.SizeBytes is < 1 or > MaximumBytes)
            || eligible.Sum(attachment => attachment.SizeBytes) > MaximumBytes || eligible.Select(attachment => attachment.Id).Distinct(StringComparer.Ordinal).Count() != eligible.Length)
            throw new ConnectorProviderException("FinancialSourceAttachmentLimit");
        List<FinancialDocumentAttachment> result = [];
        foreach (GmailAttachmentMetadata attachment in eligible)
        {
            GmailAttachmentResult downloaded = ConnectorJson.Read<GmailAttachmentResult>(await connectors.InvokeSourceReadAsync(binding,
                new(GmailActionKeys.GetAttachment, 1), ConnectorJson.Element(new GmailGetAttachmentRequest(messageId, attachment.Id)), token));
            ConnectorStoredContent metadata = content.Describe(downloaded.FileReference);
            if (downloaded.MessageId != messageId || downloaded.AttachmentId != attachment.Id || metadata.ConnectionId != connection.Id
                || metadata.MessageId != messageId || metadata.AttachmentId != attachment.Id || metadata.ContentType != attachment.ContentType
                || downloaded.ContentType != metadata.ContentType || metadata.SizeBytes != attachment.SizeBytes || downloaded.SizeBytes != metadata.SizeBytes
                || downloaded.Sha256 != metadata.Sha256 || metadata.ExpiresAt <= clock.UtcNow)
                throw new ConnectorProviderException("FinancialSourceAttachmentMismatch");
            await authorization.RequireUseAsync(connection.Id, catalog.Resolve(new(GmailActionKeys.GetAttachment, 1)), context, token);
            await using Stream stream = await content.OpenAsync(downloaded.FileReference, token);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(chunk, token)) != 0)
            {
                if (buffer.Length + count > metadata.SizeBytes) throw new ConnectorProviderException("ContentSizeMismatch");
                await buffer.WriteAsync(chunk.AsMemory(0, count), token);
            }
            if (buffer.Length != metadata.SizeBytes || buffer.Length > MaximumBytes) throw new ConnectorProviderException("ContentSizeMismatch");
            byte[] bytes = buffer.ToArray();
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != metadata.Sha256) throw new ConnectorProviderException("ContentHashMismatch");
            result.Add(new(new(connection.Id, accountId, binding.WorkflowId, binding.WorkflowVersionId, binding.Connection.WorkflowGrantId,
                binding.AutomationIdentityId, bindingId, revision, messageId, attachment.Id, metadata.Sha256), metadata.FileName, metadata.ContentType, bytes));
        }
        _ = await bindings.RequireActiveAsync(bindingId, revision, token);
        var current = await authorization.RequireUseAsync(connection.Id, catalog.Resolve(new(GmailActionKeys.GetMessage, 1)), context, token);
        if (current.ProviderAccountId != accountId) throw ConnectorAuthorization.Access("ProviderAccountChanged");
        return result;
    }

    internal static bool Supported(string type) => type is "application/pdf" or "image/png" or "image/jpeg" or "text/plain";
}
