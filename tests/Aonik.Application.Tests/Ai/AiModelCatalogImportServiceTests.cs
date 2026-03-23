using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests;

public class AiModelCatalogImportServiceTests
{
    private static AiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"AiModelCatalogImport_{Guid.NewGuid()}")
            .Options;

        return new AiDbContext(options);
    }

    private static AiModelCatalogImportService CreateService(AiDbContext dbContext, IAiModelCatalogSource catalogSource)
    {
        return new AiModelCatalogImportService(
            dbContext,
            catalogSource,
            NullLogger<AiModelCatalogImportService>.Instance);
    }

    [Fact]
    public async Task ImportModelProviderAsync_ShouldCreateProviderAndInactiveModels_WhenProviderDoesNotExist()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var catalogSource = new StubAiModelCatalogSource(
            new AiCatalogModelProviderResponse
            {
                ModelProviderKey = "openai",
                Name = "OpenAI",
                DocumentationUrl = "https://platform.openai.com/docs/models",
                SdkPackage = "@ai-sdk/openai",
                ApiBaseUrl = null,
                EnvironmentVariables = ["OPENAI_API_KEY"],
                ModelCount = 2,
            },
            [
                new AiCatalogModelResponse
                {
                    ModelProviderKey = "openai",
                    ModelKey = "gpt-4o-mini",
                    Name = "GPT-4o mini",
                    Family = "gpt",
                    ContextWindow = 128000,
                    OutputTokenLimit = 16384,
                    CostProfileJson = "{}",
                    InputModalities = ["text"],
                    OutputModalities = ["text"],
                    SupportsReasoning = false,
                    SupportsToolCall = true,
                    SupportsStructuredOutput = true,
                    SupportsAttachments = false,
                    IsOpenWeights = false,
                },
                new AiCatalogModelResponse
                {
                    ModelProviderKey = "openai",
                    ModelKey = "gpt-4.1",
                    Name = "GPT-4.1",
                    Family = "gpt",
                    ContextWindow = 1048576,
                    OutputTokenLimit = 32768,
                    CostProfileJson = "{}",
                    InputModalities = ["text", "image"],
                    OutputModalities = ["text"],
                    SupportsReasoning = true,
                    SupportsToolCall = true,
                    SupportsStructuredOutput = true,
                    SupportsAttachments = true,
                    IsOpenWeights = false,
                },
            ]);
        var service = CreateService(dbContext, catalogSource);

        // Act
        var result = await service.ImportModelProviderAsync("openai", new ImportAiCatalogModelProviderRequest());

        // Assert
        result.ProviderCreated.Should().BeTrue();
        result.ModelsCreated.Should().Be(2);
        result.ModelsLinked.Should().Be(0);
        result.ModelsSkipped.Should().Be(0);

        var provider = await dbContext.AiProviders.SingleAsync();
        provider.Name.Should().Be("OpenAI");
        provider.ExternalModelProviderKey.Should().Be("openai");
        provider.IsActive.Should().BeFalse();

        var models = await dbContext.AiModels.OrderBy(model => model.ModelName).ToListAsync();
        models.Should().HaveCount(2);
        models.Select(model => model.ModelName).Should().BeEquivalentTo(["gpt-4.1", "gpt-4o-mini"]);
        models.Should().OnlyContain(model => model.IsActive == false);
        models.Should().OnlyContain(model => model.ExternalModelKey != null);
    }

    [Fact]
    public async Task ImportModelProviderAsync_ShouldReuseExistingProviderAndLinkExistingModel_WhenNamesMatch()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var existingProvider = new AiProvider
        {
            Name = "OpenAI",
            CapabilitiesJson = "{}",
            IsActive = true,
        };
        dbContext.AiProviders.Add(existingProvider);
        dbContext.AiModels.Add(new AiModel
        {
            AiProviderId = existingProvider.Id,
            ModelName = "gpt-4o-mini",
            ContextWindow = 128000,
            CostProfileJson = "{}",
            LatencyProfileJson = "{}",
            PolicyTagsJson = "[]",
            IsActive = true,
        });
        await dbContext.SaveChangesAsync();

        var catalogSource = new StubAiModelCatalogSource(
            new AiCatalogModelProviderResponse
            {
                ModelProviderKey = "openai",
                Name = "OpenAI",
                DocumentationUrl = null,
                SdkPackage = null,
                ApiBaseUrl = null,
                EnvironmentVariables = [],
                ModelCount = 2,
            },
            [
                new AiCatalogModelResponse
                {
                    ModelProviderKey = "openai",
                    ModelKey = "gpt-4o-mini",
                    Name = "GPT-4o mini",
                    Family = "gpt",
                    ContextWindow = 128000,
                    OutputTokenLimit = 16384,
                    CostProfileJson = "{}",
                    InputModalities = ["text"],
                    OutputModalities = ["text"],
                    SupportsReasoning = false,
                    SupportsToolCall = true,
                    SupportsStructuredOutput = true,
                    SupportsAttachments = false,
                    IsOpenWeights = false,
                },
                new AiCatalogModelResponse
                {
                    ModelProviderKey = "openai",
                    ModelKey = "gpt-5-mini",
                    Name = "GPT-5 mini",
                    Family = "gpt",
                    ContextWindow = 400000,
                    OutputTokenLimit = 128000,
                    CostProfileJson = "{}",
                    InputModalities = ["text"],
                    OutputModalities = ["text"],
                    SupportsReasoning = true,
                    SupportsToolCall = true,
                    SupportsStructuredOutput = true,
                    SupportsAttachments = false,
                    IsOpenWeights = false,
                },
            ]);
        var service = CreateService(dbContext, catalogSource);

        // Act
        var result = await service.ImportModelProviderAsync("openai", new ImportAiCatalogModelProviderRequest());

        // Assert
        result.ProviderCreated.Should().BeFalse();
        result.ModelsCreated.Should().Be(1);
        result.ModelsLinked.Should().Be(1);
        result.ModelsSkipped.Should().Be(0);

        var provider = await dbContext.AiProviders.SingleAsync();
        provider.Id.Should().Be(existingProvider.Id);
        provider.ExternalModelProviderKey.Should().Be("openai");

        var models = await dbContext.AiModels.OrderBy(model => model.ModelName).ToListAsync();
        models.Should().HaveCount(2);
        models.Single(model => model.ModelName == "gpt-4o-mini").ExternalModelKey.Should().Be("gpt-4o-mini");
        models.Single(model => model.ModelName == "gpt-5-mini").ExternalModelKey.Should().Be("gpt-5-mini");
    }

    private sealed class StubAiModelCatalogSource : IAiModelCatalogSource
    {
        private readonly AiCatalogModelProviderResponse _modelProvider;
        private readonly IReadOnlyList<AiCatalogModelResponse> _models;

        public StubAiModelCatalogSource(
            AiCatalogModelProviderResponse modelProvider,
            IReadOnlyList<AiCatalogModelResponse> models)
        {
            _modelProvider = modelProvider;
            _models = models;
        }

        public Task<IReadOnlyList<AiCatalogModelProviderResponse>> ListModelProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AiCatalogModelProviderResponse>>([_modelProvider]);

        public Task<AiCatalogModelProviderResponse?> GetModelProviderAsync(string modelProviderKey, CancellationToken ct = default)
        {
            var result = string.Equals(_modelProvider.ModelProviderKey, modelProviderKey, StringComparison.OrdinalIgnoreCase)
                ? _modelProvider
                : null;

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<AiCatalogModelResponse>> ListModelsAsync(string modelProviderKey, CancellationToken ct = default)
        {
            IReadOnlyList<AiCatalogModelResponse> result = string.Equals(_modelProvider.ModelProviderKey, modelProviderKey, StringComparison.OrdinalIgnoreCase)
                ? _models
                : [];

            return Task.FromResult(result);
        }
    }
}
