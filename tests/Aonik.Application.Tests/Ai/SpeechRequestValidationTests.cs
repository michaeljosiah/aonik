using Aonik.Ai.Endpoints.Safety;
using FluentAssertions;

namespace Aonik.Application.Tests.Ai;

public class SpeechRequestValidationTests
{
    [Theory]
    [InlineData("output", "data:audio/wav;base64,AQID", true)]
    [InlineData("output", "data:audio/mpeg;base64,AQID", true)]
    [InlineData("input", "data:audio/wav;base64,AQID", false)]
    [InlineData("output", "https://example.com/audio.wav", false)]
    [InlineData("output", "data:audio/ogg;base64,AQID", false)]
    public void Speech_requires_inline_supported_output(string layer, string content, bool valid)
    {
        var result = new ScreenContentRequestValidator().Validate(
            new ScreenContentRequest(Guid.NewGuid(), "speech", layer, content));
        result.IsValid.Should().Be(valid);
    }
}
