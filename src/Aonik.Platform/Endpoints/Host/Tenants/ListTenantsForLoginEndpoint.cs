using FastEndpoints;

using Aonik.Platform.Contracts.Api.Host;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Host.Tenants;

/// <summary>
/// Public endpoint to list active tenants for login dropdown.
/// No authentication required - returns minimal tenant info.
/// </summary>
internal class ListTenantsForLoginEndpoint : EndpointWithoutRequest<TenantListForLoginResponse>
{
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<ListTenantsForLoginEndpoint> _logger;

    public ListTenantsForLoginEndpoint(
        PlatformDbContext dbContext,
        ILogger<ListTenantsForLoginEndpoint> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/host/tenants/list-for-login");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "List tenants for login";
            s.Description = "Returns a list of active tenants for display in the login tenant selector dropdown.";
            s.Response(200, "Tenant list returned");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var tenants = await _dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.Status == TenantStatus.Active)
                .OrderBy(t => t.Name)
                .Select(t => new TenantListItemForLoginResponse(
                    t.Id,
                    t.Name,
                    t.Subdomain,
                    t.Environment))
                .ToListAsync(linkedCts.Token);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            await Send.OkAsync(new TenantListForLoginResponse(tenants), ct);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Host tenant login list query was cancelled or timed out.");

            if (ct.IsCancellationRequested)
            {
                return;
            }

            await Send.OkAsync(new TenantListForLoginResponse([]), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tenants for login; returning an empty list.");

            if (ct.IsCancellationRequested)
            {
                return;
            }

            await Send.OkAsync(new TenantListForLoginResponse([]), CancellationToken.None);
        }
    }
}
