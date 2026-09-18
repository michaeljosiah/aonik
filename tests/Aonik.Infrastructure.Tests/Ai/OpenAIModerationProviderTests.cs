using System.Net;
using System.Text.Json;

using Aonik.Infrastructure.Ai.Safety;
using Aonik.SharedKernel.Abstractions.Safety;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Tests.Ai;

/// <summary>
/// The one supported classification route (aonik#323), against its wire shape: what is sent to the
/// moderation endpoint, how its taxonomy lands on Spec 096's categories, and that nothing passes
/// when the endpoint does not answer.
/// </summary>
public class OpenAIModerationProviderTests
{
    [Fact]
    public async Task Text_Should_BeSentWholeWithTheRoutedModel_AndScoresMappedOntoSpec096Categories()
    {
        var handler = new ScriptedHandler(HttpStatusCode.OK, """
            {"id":"modr-1","model":"omni-moderation-latest","results":[{"flagged":true,
              "categories":{"violence":true},
              "category_scores":{"sexual":0.01,"sexual/minors":0.0,"violence":0.91,"violence/graphic":0.40,
                "self-harm":0.02,"self-harm/intent":0.05,"self-harm/instructions":0.0,
                "hate":0.03,"hate/threatening":0.01,"harassment":0.12,"harassment/threatening":0.0,
                "illicit":0.2,"illicit/violent":0.3}}]}
            """);
        var provider = Provider(handler, apiKey: "sk-test");

        var scores = await provider.ScoreAsync(SafetyModalities.Text, "There was blood everywhere.", SafetyBandNames.Under6, "omni-moderation-latest");

        handler.Request!.RequestUri!.ToString().Should().Be("https://api.openai.com/v1/moderations");
        handler.Request.Headers.Authorization!.Parameter.Should().Be("sk-test");
        var body = JsonDocument.Parse(handler.Body!).RootElement;
        body.GetProperty("model").GetString().Should().Be("omni-moderation-latest");
        body.GetProperty("input").GetString().Should().Be("There was blood everywhere.");

        // A Spec 096 category takes the highest of the provider categories mapped onto it.
        scores[SafetyCategories.GraphicViolence].Should().Be(0.91);
        scores[SafetyCategories.SelfHarm].Should().Be(0.05);
        scores[SafetyCategories.Hate].Should().Be(0.12);
        scores[SafetyCategories.Sexual].Should().Be(0.01);
        scores[SafetyCategories.Csam].Should().Be(0.0);
        scores.Should().NotContainKey(SafetyCategories.Frightening, "no provider category stands for it; the gate's other layers do");
    }

    [Fact]
    public async Task AnImage_Should_BeSentAsAnImageUrl()
    {
        var handler = new ScriptedHandler(HttpStatusCode.OK, """{"results":[{"flagged":false,"categories":{},"category_scores":{"sexual":0.0}}]}""");
        var provider = Provider(handler, apiKey: "sk-test");

        await provider.ScoreAsync(SafetyModalities.Image, "https://cdn.example/pip.png", SafetyBandNames.Age6To9, "omni-moderation-latest");

        var input = JsonDocument.Parse(handler.Body!).RootElement.GetProperty("input");
        input[0].GetProperty("type").GetString().Should().Be("image_url");
        input[0].GetProperty("image_url").GetProperty("url").GetString().Should().Be("https://cdn.example/pip.png");
    }

    [Fact]
    public async Task NoKey_OrNoAnswer_Should_Throw_SoTheGateRefuses()
    {
        await FluentActions.Awaiting(() => Provider(new ScriptedHandler(HttpStatusCode.OK, "{}"), apiKey: null)
                .ScoreAsync(SafetyModalities.Text, "Pip.", SafetyBandNames.Under6, "omni-moderation-latest"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*no API key*");

        await FluentActions.Awaiting(() => Provider(new ScriptedHandler(HttpStatusCode.TooManyRequests, """{"error":{"message":"slow down"}}"""), apiKey: "sk-test")
                .ScoreAsync(SafetyModalities.Text, "Pip.", SafetyBandNames.Under6, "omni-moderation-latest"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*429*");

        await FluentActions.Awaiting(() => Provider(new ScriptedHandler(HttpStatusCode.OK, """{"results":[]}"""), apiKey: "sk-test")
                .ScoreAsync(SafetyModalities.Text, "Pip.", SafetyBandNames.Under6, "omni-moderation-latest"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*no result*");
    }

    [Fact]
    public void TheRoute_Should_DeclareTextAndImage_WholeNotSampled()
    {
        var provider = Provider(new ScriptedHandler(HttpStatusCode.OK, "{}"), apiKey: "sk-test");

        provider.Provider.Should().Be("openai");
        provider.SupportedModalities.Should().BeEquivalentTo([SafetyModalities.Text, SafetyModalities.Image]);
        provider.Coverage.Should().Be(TemporalCoverage.Complete);
    }

    private static OpenAIModerationProvider Provider(ScriptedHandler handler, string? apiKey)
        => new(new HttpClient(handler), Options.Create(new OpenAIModerationOptions { ApiKey = apiKey }), NullLogger<OpenAIModerationProvider>.Instance);

    private sealed class ScriptedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
