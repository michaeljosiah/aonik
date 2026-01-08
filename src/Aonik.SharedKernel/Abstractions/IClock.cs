namespace Aonik.SharedKernel.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
