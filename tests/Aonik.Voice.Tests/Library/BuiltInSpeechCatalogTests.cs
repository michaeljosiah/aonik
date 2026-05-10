using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.Voice.Library;
using FluentAssertions;

namespace Aonik.Voice.Tests.Library;

/// <summary>
/// Verifies the immutable in-code catalog: stable ids, IsBuiltIn=true everywhere, the right
/// config type per (Type, Vendor), and the reserved id prefix.
/// </summary>
public class BuiltInSpeechCatalogTests
{
    private readonly BuiltInSpeechCatalog _catalog = new();

    [Fact]
    public void Catalog_Ships_Eight_Archetypes()
    {
        // Spec 024 §"Built-in archetypes" — 2 STT + 4 TTS + 2 Composite = 8.
        _catalog.AllProviders.Should().HaveCount(8);
    }

    [Fact]
    public void Every_Built_In_Has_The_Reserved_Id_Prefix()
    {
        _catalog.AllProviders.Should().OnlyContain(p =>
            p.Id.StartsWith(SpeechLibraryConstants.BuiltInIdPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_Built_In_Is_Marked_IsBuiltIn_And_Active_And_Version_One()
    {
        _catalog.AllProviders.Should().OnlyContain(p =>
            p.IsBuiltIn
            && p.Status == SpeechProviderStatus.Active
            && p.Version == 1);
    }

    [Theory]
    [InlineData("built-in:openai-whisper-default", SpeechProviderType.Stt, "openai", typeof(OpenAIWhisperConfig))]
    [InlineData("built-in:azure-stt-en-us-eastus", SpeechProviderType.Stt, "azure", typeof(AzureSttConfig))]
    [InlineData("built-in:openai-tts-alloy", SpeechProviderType.Tts, "openai", typeof(OpenAITtsConfig))]
    [InlineData("built-in:openai-tts-onyx-hd", SpeechProviderType.Tts, "openai", typeof(OpenAITtsConfig))]
    [InlineData("built-in:azure-tts-jenny-eastus", SpeechProviderType.Tts, "azure", typeof(AzureTtsConfig))]
    [InlineData("built-in:elevenlabs-rachel", SpeechProviderType.Tts, "elevenlabs", typeof(ElevenLabsTtsConfig))]
    [InlineData("built-in:openai-realtime", SpeechProviderType.Composite, "openai-realtime", typeof(OpenAIRealtimeCompositeConfig))]
    [InlineData("built-in:azure-voice-live-uksouth", SpeechProviderType.Composite, "azure-voice-live", typeof(AzureVoiceLiveCompositeConfig))]
    public void Built_In_Has_Expected_Type_Vendor_And_Config_Shape(
        string id, SpeechProviderType expectedType, string expectedVendor, Type expectedConfigType)
    {
        var p = _catalog.FindProvider(id);
        p.Should().NotBeNull();
        p!.Type.Should().Be(expectedType);
        p.Vendor.Should().Be(expectedVendor);
        p.Config.Should().BeOfType(expectedConfigType);
    }

    [Fact]
    public void FindProvider_Returns_Null_For_Unknown_Id()
    {
        _catalog.FindProvider("built-in:does-not-exist").Should().BeNull();
        _catalog.FindProvider("not-a-built-in").Should().BeNull();
    }

    [Fact]
    public void Built_In_Ids_Are_Unique()
    {
        var ids = _catalog.AllProviders.Select(p => p.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }
}
