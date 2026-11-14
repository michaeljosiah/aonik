namespace Aonik.SharedKernel.Abstractions.Documents;

public sealed class DocumentTextExtractionException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}
