using System.Text.Json;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services.Safety;

/// <summary>The composite routes and checks consent before entering this adapter; no content is logged.</summary>
internal sealed class RecordedOpenAiSpeechTranscriber(
    IEnumerable<ISpeechTranscriptionProvider> providers, AiDbContext db,
    ITenantProvider tenantProvider, IClock clock) : ISpeechTranscriber
{
    public string Provider => "openai";
    private ISpeechTranscriptionProvider Transport => providers.Single(p => p.Provider == Provider);
    public TemporalCoverage Coverage => Transport.Coverage;

    public async Task<SpeechTranscript> TranscribeAsync(Guid subjectPartyId, string reference,
        string modelName, CancellationToken cancellationToken = default)
    {
        var model = await db.AiModels.AsNoTracking().SingleOrDefaultAsync(m => m.ModelName == modelName && m.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("The routed transcription model is not registered.");
        var started = clock.UtcNow;
        var run = new AiRun {
            Id = Guid.NewGuid(), TenantId = tenantProvider.GetCurrentTenantId(), AiModelId = model.Id,
            UseCase = SafetyUseCases.TranscribeSpeech, Outcome = "Started",
            InputRefsJson = JsonSerializer.Serialize(new { subject = subjectPartyId, promptVersion = "speech-transcript-v1" })
        };
        db.AiRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var text = await Transport.TranscribeAsync(reference, modelName, cancellationToken);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("No intelligible transcript was returned.");
            run.Outcome = "Completed"; run.LatencyMs = (int)(clock.UtcNow - started).TotalMilliseconds;
            await db.SaveChangesAsync(cancellationToken);
            return new SpeechTranscript(text, run.Id);
        }
        catch (Exception error)
        {
            run.Outcome = "Failed"; run.LatencyMs = (int)(clock.UtcNow - started).TotalMilliseconds;
            await db.SaveChangesAsync(CancellationToken.None);
            throw new SpeechClassificationFailedException("Speech transcription failed.", [run.Id], error);
        }
    }
}
