using Aonik.Ai.Services;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Ai;

public class FileBasedPromptStoreTests
{
    private string GetTemplatesPath()
    {
        // Get the path to the Infrastructure project's prompt templates
        var currentDirectory = Directory.GetCurrentDirectory();
        var solutionRoot = Directory.GetParent(currentDirectory)!.Parent!.Parent!.Parent!.Parent!.FullName;
        return Path.Combine(solutionRoot, "src", "Aonik.Ai", "Prompting", "Templates");
    }

    [Fact]
    public async Task LoadPromptAsync_ShouldLoadSystemPrompt()
    {
        // Arrange
        var templatesPath = GetTemplatesPath();
        var store = new FileBasedPromptStore(templatesPath);

        // Act
        var prompt = await store.LoadPromptAsync("invoice_insight", "v1", "system");

        // Assert
        prompt.Should().NotBeNullOrEmpty();
        prompt.Should().ContainAny("invoice", "Invoice");
    }

    [Fact]
    public async Task LoadPromptAsync_ShouldLoadUserPrompt()
    {
        // Arrange
        var templatesPath = GetTemplatesPath();
        var store = new FileBasedPromptStore(templatesPath);

        // Act
        var prompt = await store.LoadPromptAsync("invoice_insight", "v1", "user");

        // Assert
        prompt.Should().NotBeNullOrEmpty();
        prompt.Should().Contain("{{INVOICE_DATA}}");
    }

    [Fact]
    public async Task LoadPromptAsync_ShouldThrow_WhenFileNotFound()
    {
        // Arrange
        var templatesPath = GetTemplatesPath();
        var store = new FileBasedPromptStore(templatesPath);

        // Act
        var act = async () => await store.LoadPromptAsync("nonexistent", "v1", "system");

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadPromptAsync_ShouldLoadDifferentVersions()
    {
        // Arrange
        var templatesPath = GetTemplatesPath();
        var store = new FileBasedPromptStore(templatesPath);

        // Act
        var v1System = await store.LoadPromptAsync("invoice_insight", "v1", "system");
        var v1User = await store.LoadPromptAsync("invoice_insight", "v1", "user");

        // Assert
        v1System.Should().NotBeNullOrEmpty();
        v1User.Should().NotBeNullOrEmpty();
        v1System.Should().NotBe(v1User);
    }

    [Fact]
    public async Task LoadPromptAsync_ShouldHandleMultilineContent()
    {
        // Arrange
        var templatesPath = GetTemplatesPath();
        var store = new FileBasedPromptStore(templatesPath);

        // Act
        var prompt = await store.LoadPromptAsync("invoice_insight", "v1", "system");

        // Assert
        prompt.Should().Contain("\n", "multiline prompts should preserve line breaks");
    }
}
