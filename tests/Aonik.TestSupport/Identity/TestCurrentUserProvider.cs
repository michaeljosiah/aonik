using Aonik.SharedKernel.Abstractions;

namespace Aonik.TestSupport.Identity;

/// <summary>
/// In-memory <see cref="ICurrentUserProvider"/> that always reports a
/// fixed user id. Convenience constructor generates a fresh Guid for
/// tests that don't share a user with anyone else.
/// </summary>
public sealed class TestCurrentUserProvider : ICurrentUserProvider
{
    private readonly Guid _userId;

    public TestCurrentUserProvider(Guid userId) => _userId = userId;

    public TestCurrentUserProvider() : this(Guid.NewGuid()) { }

    public Guid UserId => _userId;

    public Guid? GetCurrentUserId() => _userId;

    public bool TryGetCurrentUserId(out Guid userId)
    {
        userId = _userId;
        return true;
    }
}
