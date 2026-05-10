using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin;

internal sealed class ListVoiceProviderVoicesRequest
{
    public string? Provider { get; set; }
}

internal sealed class ListVoiceProviderVoicesEndpoint : Endpoint<ListVoiceProviderVoicesRequest, IReadOnlyList<VoiceOptionResponse>>
{
    public override void Configure()
    {
        Get("/tenant/settings/voice/voices");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List voices for a provider";
            s.Description = "Returns the voice options the admin UI's voice picker can show for the given provider.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(ListVoiceProviderVoicesRequest req, CancellationToken ct)
    {
        var voices = VoiceRecipeCatalog.VoicesFor(req.Provider ?? "openai");
        await Send.OkAsync(voices, ct);
    }
}
