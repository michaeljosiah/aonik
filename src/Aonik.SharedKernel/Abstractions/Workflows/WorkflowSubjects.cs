namespace Aonik.SharedKernel.Abstractions.Workflows;

public static class WorkflowSubjects
{
    public const string Invoice = "Invoice";
    public const string Document = "Document";
    public static bool IsSupported(string? subjectType) => subjectType is Invoice or Document;
}
