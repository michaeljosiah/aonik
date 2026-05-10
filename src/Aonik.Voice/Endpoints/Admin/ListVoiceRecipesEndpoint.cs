using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin;

internal sealed class ListVoiceRecipesEndpoint : EndpointWithoutRequest<IReadOnlyList<VoiceRecipeResponse>>
{
    public override void Configure()
    {
        Get("/tenant/settings/voice/recipes");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List voice recipes";
            s.Description = "Returns the curated voice recipes available for v1 (and previews of v1.1 recipes flagged as not implemented).";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(VoiceRecipeCatalog.All, ct);
    }
}
