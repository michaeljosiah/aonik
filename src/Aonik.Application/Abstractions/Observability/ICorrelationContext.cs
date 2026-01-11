namespace Aonik.Application.Abstractions.Observability;

public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
