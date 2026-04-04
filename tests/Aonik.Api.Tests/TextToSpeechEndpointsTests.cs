using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Aonik.Ai.Providers;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;

namespace Aonik.Api.Tests;

public class TextToSpeechEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TextToSpeechEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MobileTextToSpeechSynthesize_ReturnsAudioBytesAndHeaders()
    {
        var auth = TestAuthOptions.Create().WithRoles("PersonalUser");

        await using var factory = new TextToSpeechTestWebApplicationFactory();
        var client = await factory.CreateAuthenticatedClientAsync(auth);
        await factory.EnableTenantTextToSpeechAsync(auth.TenantId!.Value, "voice_123");

        var response = await client.PostAsJsonAsync(
            "/mobile/text-to-speech/synthesize",
            new
            {
                speechText = "Your transport spending increased this week.",
                locale = "en-US",
                threadId = "thread-1",
                messageId = "message-1"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("audio/mpeg");
        response.Headers.Should().Contain(header => header.Key == "X-Ai-Run-Id");
        response.Headers.Should().Contain(header => header.Key == "X-Tts-Provider");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal(Encoding.UTF8.GetBytes("fake-mp3-audio"));
    }

    [Fact]
    public async Task TenantTextToSpeechPreview_ReturnsAudioBytesAndUsesPreviewVoiceOverride()
    {
        var auth = TestAuthOptions.Create().WithRoles("TenantAdmin");

        await using var factory = new TextToSpeechTestWebApplicationFactory();
        var client = await factory.CreateAuthenticatedClientAsync(auth);
        await factory.EnableTenantTextToSpeechAsync(auth.TenantId!.Value, "tenant-default-voice");

        var response = await client.PostAsJsonAsync(
            "/tenant/settings/text-to-speech/preview",
            new
            {
                text = "Preview this tenant voice.",
                locale = "en-US",
                provider = "ElevenLabs",
                voiceId = "preview-voice-override",
                modelId = "eleven_multilingual_v2",
                outputFormat = "mp3_44100_128",
                providerOptions = new Dictionary<string, string?>
                {
                    ["optimizeStreamingLatency"] = "3"
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("audio/mpeg");
        response.Headers.Should().Contain(header => header.Key == "X-Ai-Run-Id");
        response.Headers.GetValues("X-Tts-Provider").Should().ContainSingle().Which.Should().Be("ElevenLabs");
        response.Headers.GetValues("X-Tts-Voice-Id").Should().ContainSingle().Which.Should().Be("preview-voice-override");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal(Encoding.UTF8.GetBytes("fake-mp3-audio"));
    }

    [Fact]
    public async Task AgUiStreaming_EmitsSpeechRenderCustomEvent()
    {
        var auth = TestAuthOptions.Create().WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(auth);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/ai/agui");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Content = JsonContent.Create(new
        {
            agentId = "personal-finance-agent",
            messages = new[]
            {
                new { id = "user-1", role = "user", content = "Summarize my transport spending." }
            }
        });

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"type\":\"CUSTOM\"");
        body.Should().Contain("\"name\":\"speech.render\"");
        body.Should().Contain("speechText");
    }

    private sealed class TextToSpeechTestWebApplicationFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AI:TextToSpeech:ElevenLabsBaseUrl"] = "https://api.elevenlabs.test"
                });
            });

            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ElevenLabsTextToSpeechProvider>();
                services.AddHttpClient<ElevenLabsTextToSpeechProvider>()
                    .ConfigurePrimaryHttpMessageHandler(() => new StubElevenLabsHandler());
            });
        }

        public async Task EnableTenantTextToSpeechAsync(Guid tenantId, string voiceId)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISettingValueProtector>();

            var hostCredential = await db.Settings.FirstOrDefaultAsync(item =>
                item.Key == TextToSpeechSettingNames.ElevenLabsApiKey
                && item.Scope == SettingScope.Global
                && item.TenantId == null
                && item.UserId == null);

            if (hostCredential == null)
            {
                db.Settings.Add(new Setting
                {
                    Key = TextToSpeechSettingNames.ElevenLabsApiKey,
                    Scope = SettingScope.Global,
                    TenantId = null,
                    UserId = null,
                    Value = protector.Protect("test-api-key")
                });
            }
            else
            {
                hostCredential.Value = protector.Protect("test-api-key");
            }

            var existing = await db.Settings.FirstOrDefaultAsync(item =>
                item.Key == TextToSpeechSettingNames.TenantProfile
                && item.Scope == SettingScope.Tenant
                && item.TenantId == tenantId);

            var payload = $$"""
                {
                  "enabled": true,
                  "fallbackToNativeOnFailure": true,
                  "defaultProfile": {
                    "provider": "ElevenLabs",
                    "voiceId": "{{voiceId}}",
                    "modelId": "eleven_multilingual_v2",
                    "locale": "en-US",
                    "outputFormat": "mp3_44100_128",
                    "providerOptions": {}
                  },
                  "policy": {
                    "maxCharactersPerUtterance": 280,
                    "maxRequestsPerMinutePerUser": 20,
                    "monthlyCharacterBudget": null
                  }
                }
                """;

            if (existing == null)
            {
                db.Settings.Add(new Setting
                {
                    Key = TextToSpeechSettingNames.TenantProfile,
                    Scope = SettingScope.Tenant,
                    TenantId = tenantId,
                    Value = payload
                });
            }
            else
            {
                existing.Value = payload;
            }

            await db.SaveChangesAsync();
        }
    }

    private sealed class StubElevenLabsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.Contains("/v2/voices", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"voices\":[]}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("fake-mp3-audio"))
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("audio/mpeg") }
                }
            });
        }
    }
}
