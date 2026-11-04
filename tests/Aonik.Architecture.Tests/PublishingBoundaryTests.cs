using FluentAssertions;

using Aonik.Publishing;

namespace Aonik.Architecture.Tests;

public class PublishingBoundaryTests
{
    [Fact]
    public void Publishing_Should_ReferenceOnlySharedKernel()
    {
        typeof(PublishingModule).Assembly.GetReferencedAssemblies()
            .Where(x => x.Name!.StartsWith("Aonik.", StringComparison.Ordinal))
            .Select(x => x.Name).Should().Equal("Aonik.SharedKernel");
    }
}
