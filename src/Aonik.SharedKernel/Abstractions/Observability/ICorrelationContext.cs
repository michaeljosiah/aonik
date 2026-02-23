namespace Aonik.SharedKernel.Abstractions.Observability;

public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
