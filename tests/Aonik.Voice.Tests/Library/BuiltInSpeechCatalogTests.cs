using Aonik.Voice.Library;
using FluentAssertions;

namespace Aonik.Voice.Tests.Library;

/// <summary>
/// The hard-coded archetype catalog was removed when the speech library moved to a
/// "create-your-own" flow. These tests pin the catalog as intentionally empty so a
/// future refactor doesn't accidentally re-introduce shipped built-ins.
/// </summary>
public class BuiltInSpeechCatalogTests
{
    private readonly BuiltInSpeechCatalog _catalog = new();

    [Fact]
    public void Catalog_Ships_No_Built_In_Providers()
    {
        _catalog.AllProviders.Should().BeEmpty();
    }

    [Fact]
    public void Catalog_Ships_No_Built_In_Recipes()
    {
        _catalog.AllRecipes.Should().BeEmpty();
    }

    [Fact]
    public void FindProvider_Always_Returns_Null()
    {
        _catalog.FindProvider("built-in:openai-whisper-default").Should().BeNull();
        _catalog.FindProvider("not-a-built-in").Should().BeNull();
    }

    [Fact]
    public void FindRecipe_Always_Returns_Null()
    {
        _catalog.FindRecipe("built-in:cost-chained-openai").Should().BeNull();
        _catalog.FindRecipe("not-a-built-in").Should().BeNull();
    }
}
