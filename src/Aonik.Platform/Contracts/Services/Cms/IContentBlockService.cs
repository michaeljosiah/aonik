using Aonik.Platform.Contracts.Models.Cms;

namespace Aonik.Platform.Contracts.Services.Cms;

public interface IContentBlockService
{
    Task<ContentBlockResponse> CreateContentBlockAsync(
        CreateContentBlockRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentBlockResponse?> GetContentBlockAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentBlockResponse?> GetContentBlockByKeyAsync(
        string contentKey,
        string locale,
        CancellationToken cancellationToken = default);

    Task<List<ContentBlockResponse>> ListContentBlocksAsync(
        ContentBlockListRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentBlockResponse> UpdateContentBlockAsync(
        Guid id,
        UpdateContentBlockRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteContentBlockAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentBlockMediaResponse> AddMediaAsync(
        Guid contentBlockId,
        AddContentBlockMediaRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMediaAsync(
        Guid contentBlockId,
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task ReorderMediaAsync(
        Guid contentBlockId,
        List<Guid> mediaIdsInOrder,
        CancellationToken cancellationToken = default);

    Task<List<ContentBlockResponse>> GetActiveContentBlocksAsync(
        string area,
        string locale,
        CancellationToken cancellationToken = default);
}
