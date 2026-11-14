using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Documents.Services;

internal sealed class FinancialDocumentToolApprovalManifest : IToolApprovalManifest
{
    public string Module => "Documents";
    public ToolClassification? Classify(string toolName) => toolName == FinancialDocumentActions.ExtractTool
        ? ToolClassification.Mutating(FinancialDocumentWorkflowAction.Policy) : null;
}
