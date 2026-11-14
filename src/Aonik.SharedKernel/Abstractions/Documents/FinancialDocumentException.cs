namespace Aonik.SharedKernel.Abstractions.Documents;

public sealed class FinancialDocumentException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}
