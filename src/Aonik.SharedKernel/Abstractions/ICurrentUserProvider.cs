namespace Aonik.SharedKernel.Abstractions;

public interface ICurrentUserProvider
{
    Guid? GetCurrentUserId();
    bool TryGetCurrentUserId(out Guid userId);
}
