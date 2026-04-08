using Aonik.Platform.Contracts.Api.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

public class CreateTextToSpeechVoiceEndpoint : Endpoint<CreateTextToSpeechVoiceRequest, CreateTextToSpeechVoiceResponse>
{
    private readonly ITextToSpeechService _textToSpeechService;

    public CreateTextToSpeechVoiceEndpoint(ITextToSpeechService textToSpeechService)
    {
        _textToSpeechService = textToSpeechService;
    }

    public override void Configure()
    {
        Post("/tenant/settings/text-to-speech/voices");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Create a TTS voice";
            s.Description = "Creates a new text-to-speech voice from a sample audio clip. Supported by providers that offer voice cloning (e.g. Mistral).";
            s.Response(201, "Voice created");
            s.Response(400, "Invalid request or provider does not support voice creation");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CreateTextToSpeechVoiceRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _textToSpeechService.CreateVoiceAsync(
                new TextToSpeechVoiceCreationRequest(
                    req.Provider,
                    req.Name,
                    req.SampleAudioBase64,
                    req.SampleFilename,
                    req.Languages,
                    req.Gender,
                    req.Age,
                    req.Tags),
                ct);

            await Send.CreatedAtAsync<GetTenantTextToSpeechVoicesEndpoint>(
                routeValues: null,
                responseBody: new CreateTextToSpeechVoiceResponse(result.VoiceId, result.Name, result.Provider),
                cancellation: ct);
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
