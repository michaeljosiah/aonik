namespace Aonik.SharedKernel.Abstractions.Messaging;

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>The transport definitively rejected the email before accepting delivery.</summary>
public sealed class EmailDeliveryNotStartedException(string message) : InvalidOperationException(message);
