using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Ai;

public class RecordedSpeechTranscriberTests
{
    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
    private sealed class Transport(string text) : ISpeechTranscriptionProvider
    {
        public string Provider => "openai";
        public TemporalCoverage Coverage => TemporalCoverage.Complete;
        public Task<string> TranscribeAsync(string reference, string modelName, CancellationToken cancellationToken = default)
            => Task.FromResult(text);
    }

    [Theory]
    [InlineData("A small robot planted a flower.", "Completed")]
    [InlineData("", "Failed")]
    public async Task Records_actual_routed_model_and_retains_failed_run(string transcript, string outcome)
    {
        var tenant = new TestTenantProvider(Guid.NewGuid());
        var clock = new Clock();
        using var db = new AiDbContext(new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenant, new TestCurrentUserProvider(Guid.NewGuid()), clock);
        var model = new AiModel { Id = Guid.NewGuid(), ModelName = "routed-audio", IsActive = true };
        db.AiModels.Add(model);
        await db.SaveChangesAsync();
        var transcriber = new RecordedOpenAiSpeechTranscriber([new Transport(transcript)], db, tenant, clock);
        var action = () => transcriber.TranscribeAsync(Guid.NewGuid(), "private-audio-reference", model.ModelName);
        if (outcome == "Failed")
            await action.Should().ThrowAsync<SpeechClassificationFailedException>();
        else
            (await action()).Text.Should().Be(transcript);
        var run = await db.AiRuns.SingleAsync();
        run.AiModelId.Should().Be(model.Id);
        run.Outcome.Should().Be(outcome);
        run.InputRefsJson.Should().Contain("speech-transcript-v1").And.NotContain("private-audio-reference");
    }
}
