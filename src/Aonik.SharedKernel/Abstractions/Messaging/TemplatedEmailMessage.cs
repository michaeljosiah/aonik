namespace Aonik.SharedKernel.Abstractions.Messaging;

/// <summary>The server chooses the template and supplies explicit dictionary values, not entities.
/// Tenant and sender identity come from the trusted runtime context and configuration.</summary>
public record TemplatedEmailMessage(
    string TemplateName,
    string To,
    IReadOnlyDictionary<string, object?> Model);
