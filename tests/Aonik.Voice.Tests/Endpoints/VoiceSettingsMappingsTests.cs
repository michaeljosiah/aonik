using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.Voice.Endpoints.Admin;
using FluentAssertions;

namespace Aonik.Voice.Tests.Endpoints;

public class VoiceSettingsMappingsTests
{
    [Fact]
    public void RoundTrip_Preserves_All_Settings()
    {
        // Admin UI workflow: GET → user edits → PUT. The mapping must be lossless
        // so the front-end can edit a single field without losing the others.
        var original = new VoiceProviderConfiguration(
            Enabled: true,
            Kind: VoiceProviderKind.Chained,
            RecipeId: "cost-chained-openai",
            Chained: new ChainedVoiceConfiguration(
                Stt: new SttSettings("openai-whisper", "whisper-1"),
                Tts: new TtsSettings("openai", "alloy", "tts-1"),
                Vad: new VadSettings("energy", 800),
                TranscriptionFilter: true,
                SentenceAggregator: false));

        var response = VoiceSettingsMappings.ToResponse(original);
        var update = new VoiceProviderSettingsUpdateRequest(
            Enabled: response.Enabled,
            Kind: response.Kind,
            RecipeId: response.RecipeId,
            Chained: response.Chained);
        var roundTripped = VoiceSettingsMappings.FromUpdate(update);

        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Disabled_Configuration_Maps_To_Disabled_Response()
    {
        var disabled = VoiceProviderConfiguration.Disabled;

        var response = VoiceSettingsMappings.ToResponse(disabled);

        response.Enabled.Should().BeFalse();
        response.Chained.Should().BeNull();
        response.Kind.Should().Be("chained");
    }

    [Theory]
    [InlineData("chained", VoiceProviderKind.Chained)]
    [InlineData("voice-live", VoiceProviderKind.VoiceLive)]
    [InlineData("openai-realtime", VoiceProviderKind.OpenAiRealtime)]
    [InlineData("azure-openai-realtime", VoiceProviderKind.AzureOpenAiRealtime)]
    [InlineData("CHAINED", VoiceProviderKind.Chained)]                // case-insensitive
    [InlineData("nonsense", VoiceProviderKind.Chained)]               // unknown defaults to chained for forward compatibility
    public void FromUpdate_Maps_Wire_Kind_To_Enum(string wire, VoiceProviderKind expected)
    {
        var update = new VoiceProviderSettingsUpdateRequest(
            Enabled: true,
            Kind: wire,
            RecipeId: null,
            Chained: null);

        var config = VoiceSettingsMappings.FromUpdate(update);

        config.Kind.Should().Be(expected);
    }

    [Fact]
    public void RecipeCatalog_Lists_All_Spec_Recipes()
    {
        // The four recipes named in docs/specifications/022.aonik-voice-realtime.md
        // tenant-configuration-model section. Voice mode without these surfaced is a
        // launch regression — admins won't see the v1.1 recipes as "coming soon".
        var ids = VoiceRecipeCatalog.All.Select(r => r.Id).ToArray();

        ids.Should().Contain("cost-chained-openai");
        ids.Should().Contain("premium-voice-chained");
        ids.Should().Contain("azure-only-chained");
        ids.Should().Contain("mixed-cost-optimized");
        // Added by the OpenAI catalog expansion — premium chained-OpenAI variant + Realtime composite.
        ids.Should().Contain("premium-chained-openai");
        ids.Should().Contain("openai-realtime");
    }

    [Fact]
    public void Only_OpenAI_Chained_Recipes_Are_Implemented_In_v1()
    {
        // v1 ships the chained-OpenAI recipes end-to-end (cost + premium variants of the same
        // wiring — different model ids, same factory branch). Other recipes — Voice Live,
        // OpenAI Realtime, Azure-only, mixed Azure/Mistral — surface as "Coming in v1.1".
        var implemented = VoiceRecipeCatalog.All.Where(r => r.Implemented).Select(r => r.Id).ToArray();

        implemented.Should().BeEquivalentTo(new[] { "cost-chained-openai", "premium-chained-openai" });
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("OpenAI")]
    [InlineData("openai-whisper")]
    public void OpenAI_Voices_Surface_Six_Standard_Voices(string provider)
    {
        var voices = VoiceRecipeCatalog.VoicesFor(provider);
        var ids = voices.Select(v => v.Id).ToArray();

        ids.Should().BeEquivalentTo(new[] { "alloy", "echo", "fable", "onyx", "nova", "shimmer" });
    }

    [Fact]
    public void Unknown_Provider_Returns_Empty_Voice_List()
    {
        var voices = VoiceRecipeCatalog.VoicesFor("xenovox-9000");

        voices.Should().BeEmpty();
    }
}
