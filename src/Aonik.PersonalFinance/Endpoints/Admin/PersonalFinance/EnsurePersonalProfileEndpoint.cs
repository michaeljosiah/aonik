using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Endpoints.Admin.PersonalFinance;

internal sealed class EnsurePersonalProfileRequest
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartyId { get; set; }
}

internal sealed class EnsurePersonalProfileResponse
{
    public Guid ProfileId { get; set; }
    public bool Created { get; set; }
}

internal sealed class EnsurePersonalProfileEndpoint
    : Endpoint<EnsurePersonalProfileRequest, EnsurePersonalProfileResponse>
{
    private readonly PersonalFinanceDbContext _dbContext;

    public EnsurePersonalProfileEndpoint(PersonalFinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Put("/admin/personal-finance/profiles/ensure");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Ensure a personal finance profile exists";
            s.Description = "Creates a personal finance profile for a user if one does not already exist, or updates the party link if needed.";
            s.Response(200, "Profile already exists");
            s.Response(201, "Profile created successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(EnsurePersonalProfileRequest req, CancellationToken ct)
    {
        var existing = await _dbContext.PersonalProfiles
            .FirstOrDefaultAsync(
                p => p.UserId == req.UserId && p.TenantId == req.TenantId, ct);

        if (existing != null)
        {
            if (req.PartyId != Guid.Empty && existing.PartyId != req.PartyId)
            {
                existing.PartyId = req.PartyId;
                await _dbContext.SaveChangesAsync(ct);
            }

            await Send.OkAsync(new EnsurePersonalProfileResponse
            {
                ProfileId = existing.Id,
                Created = false
            }, ct);
            return;
        }

        var profile = new PersonalProfile
        {
            TenantId = req.TenantId,
            UserId = req.UserId,
            PartyId = req.PartyId
        };

        _dbContext.PersonalProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<EnsurePersonalProfileEndpoint>(
            null,
            new EnsurePersonalProfileResponse
            {
                ProfileId = profile.Id,
                Created = true
            },
            cancellation: ct);
    }
}
