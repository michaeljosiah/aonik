namespace Aonik.Application.Services.Cms;

public interface IContentBlockService
{
    Task<Models.Cms.ContentBlockResponse> CreateContentBlockAsync(
        Models.Cms.CreateContentBlockRequest request,
        CancellationToken cancellationToken = default);

    Task<Models.Cms.ContentBlockResponse?> GetContentBlockAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Models.Cms.ContentBlockResponse?> GetContentBlockByKeyAsync(
        string contentKey,
        string locale,
        CancellationToken cancellationToken = default);

    Task<List<Models.Cms.ContentBlockResponse>> ListContentBlocksAsync(
        Models.Cms.ContentBlockListRequest request,
        CancellationToken cancellationToken = default);

    Task<Models.Cms.ContentBlockResponse> UpdateContentBlockAsync(
        Guid id,
        Models.Cms.UpdateContentBlockRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteContentBlockAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Models.Cms.ContentBlockMediaResponse> AddMediaAsync(
        Guid contentBlockId,
        Models.Cms.AddContentBlockMediaRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMediaAsync(
        Guid contentBlockId,
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task ReorderMediaAsync(
        Guid contentBlockId,
        List<Guid> mediaIdsInOrder,
        CancellationToken cancellationToken = default);

    Task<List<Models.Cms.ContentBlockResponse>> GetActiveContentBlocksAsync(
        string area,
        string locale,
        CancellationToken cancellationToken = default);
}
