using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.TestSupport.Ordering;

/// <summary>Explicit numbering boundary for fixtures exercising other order behavior.</summary>
public sealed class TestOrderNumberGenerator : IOrderNumberGenerator
{
    public Task<string> GenerateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(OrderNumberFormatting.CreateFallback(DateTime.UtcNow));
}
