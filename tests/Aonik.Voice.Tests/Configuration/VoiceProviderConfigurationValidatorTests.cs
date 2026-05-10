using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.Voice.Configuration;
using FluentAssertions;

namespace Aonik.Voice.Tests.Configuration;

public class VoiceProviderConfigurationValidatorTests
{
    private readonly VoiceProviderConfigurationValidator _validator = new();

    [Fact]
    public void Valid_Chained_OpenAI_Configuration_Passes()
    {
        var config = ValidChainedOpenAi();

        var result = _validator.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(VoiceProviderKind.VoiceLive)]
    [InlineData(VoiceProviderKind.OpenAiRealtime)]
    [InlineData(VoiceProviderKind.AzureOpenAiRealtime)]
    public void Composite_Kinds_Are_Rejected_In_v1(VoiceProviderKind kind)
    {
        var config = ValidChainedOpenAi() with { Kind = kind };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("reserved for v1.1"));
    }

    [Fact]
    public void Chained_Without_Inner_Object_Fails()
    {
        var config = new VoiceProviderConfiguration(
            Enabled: true,
            Kind: VoiceProviderKind.Chained,
            RecipeId: "test",
            Chained: null);

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("'chained' settings object is null"));
    }

    [Theory]
    [InlineData("azure")]
    [InlineData("deepgram")]
    [InlineData("")]
    public void Unwired_Stt_Vendor_Fails(string vendor)
    {
        var config = ValidChainedOpenAi();
        var c = config.Chained! with { Stt = new SttSettings(vendor, null) };
        config = config with { Chained = c };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("STT vendor") || e.Contains("'chained.stt.vendor' must"));
    }

    [Theory]
    [InlineData("elevenlabs")]
    [InlineData("mistral")]
    [InlineData("azure")]
    public void Unwired_Tts_Vendor_Fails(string vendor)
    {
        var config = ValidChainedOpenAi();
        var c = config.Chained! with { Tts = new TtsSettings(vendor, "alloy", "tts-1") };
        config = config with { Chained = c };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TTS vendor"));
    }

    [Fact]
    public void Out_Of_Range_StopMs_Fails()
    {
        var config = ValidChainedOpenAi();
        var c = config.Chained! with { Vad = new VadSettings("energy", 49) };
        config = config with { Chained = c };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("stopMs"));
    }

    [Fact]
    public void Unknown_Vad_Kind_Fails()
    {
        var config = ValidChainedOpenAi();
        var c = config.Chained! with { Vad = new VadSettings("hyperultra-vad", 800) };
        config = config with { Chained = c };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("VAD kind"));
    }

    [Fact]
    public void Null_Configuration_Fails_With_Single_Clear_Error()
    {
        var result = _validator.Validate(null!);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("must not be null"));
    }

    private static VoiceProviderConfiguration ValidChainedOpenAi() => new(
        Enabled: true,
        Kind: VoiceProviderKind.Chained,
        RecipeId: "cost-chained-openai",
        Chained: new ChainedVoiceConfiguration(
            Stt: new SttSettings("openai-whisper", "whisper-1"),
            Tts: new TtsSettings("openai", "alloy", "tts-1"),
            Vad: new VadSettings("energy", 800),
            TranscriptionFilter: true,
            SentenceAggregator: true));
}
