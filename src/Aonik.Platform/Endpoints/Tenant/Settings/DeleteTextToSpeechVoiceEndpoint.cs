using Aonik.Platform.Contracts.Api.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

public class DeleteTextToSpeechVoiceEndpoint : Endpoint<DeleteTextToSpeechVoiceRequest>
{
    private readonly ITextToSpeechService _textToSpeechService;

    public DeleteTextToSpeechVoiceEndpoint(ITextToSpeechService textToSpeechService)
    {
        _textToSpeechService = textToSpeechService;
    }

    public override void Configure()
    {
        Delete("/tenant/settings/text-to-speech/voices/{VoiceId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a TTS voice";
            s.Description = "Deletes a custom text-to-speech voice. Only supported by providers that allow voice management (e.g. Mistral).";
            s.Response(204, "Voice deleted");
            s.Response(400, "Invalid request or provider does not support voice deletion");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(DeleteTextToSpeechVoiceRequest req, CancellationToken ct)
    {
        try
        {
            await _textToSpeechService.DeleteVoiceAsync(req.Provider, req.VoiceId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (NotSupportedException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
