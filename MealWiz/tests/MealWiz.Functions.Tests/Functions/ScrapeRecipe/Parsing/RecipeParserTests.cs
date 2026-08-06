using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.ScrapeRecipe.Parsing;

public class RecipeParserTests
{
    private readonly RecipeParser _parser = new(
        new JsonLdRecipeParser(),
        new MicrodataRecipeParser(),
        new OpenGraphRecipeParser());

    private const string ExpectedInstructions = "Mix everything.\nCook on a hot pan.";

    [Fact]
    public async Task ParseAsync_JsonLd_StringInstructions()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": "Simple Pancakes",
              "recipeIngredient": ["1 cup flour", "2 eggs"],
              "recipeInstructions": "Mix everything.\nCook on a hot pan."
            }
            </script>
            </head><body></body></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Simple Pancakes", result.Name);
        Assert.Equal(ExpectedInstructions, result.Recipe);
        Assert.Equal(["1 cup flour", "2 eggs"], result.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public async Task ParseAsync_JsonLd_StringArrayInstructions()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@type": "Recipe",
              "name": "Simple Pancakes",
              "recipeIngredient": ["1 cup flour", "2 eggs"],
              "recipeInstructions": ["Mix everything.", "Cook on a hot pan."]
            }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal(ExpectedInstructions, result.Recipe);
    }

    [Fact]
    public async Task ParseAsync_JsonLd_HowToStepArrayInstructions()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@type": "Recipe",
              "name": "Simple Pancakes",
              "recipeIngredient": ["1 cup flour", "2 eggs"],
              "recipeInstructions": [
                {"@type": "HowToStep", "text": "Mix everything."},
                {"@type": "HowToStep", "text": "Cook on a hot pan."}
              ]
            }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal(ExpectedInstructions, result.Recipe);
    }

    [Fact]
    public async Task ParseAsync_JsonLd_GraphWrapper()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@context": "https://schema.org",
              "@graph": [
                { "@type": "WebSite", "name": "Some Site" },
                { "@type": "Recipe", "name": "Graph Pancakes", "recipeIngredient": ["1 cup flour"], "recipeInstructions": "Mix and cook." }
              ]
            }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Graph Pancakes", result.Name);
    }

    [Fact]
    public async Task ParseAsync_JsonLd_TopLevelArrayWithMixedTypes()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            [
              { "@type": "BreadcrumbList", "itemListElement": [] },
              { "@type": "Recipe", "name": "Array Pancakes", "recipeIngredient": ["1 cup flour"], "recipeInstructions": "Mix and cook." }
            ]
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Array Pancakes", result.Name);
    }

    [Fact]
    public async Task ParseAsync_JsonLdTakesPrecedenceOverOpenGraph()
    {
        const string html = """
            <html><head>
            <meta property="og:title" content="OG Title Should Be Ignored" />
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "JSON-LD Wins", "recipeInstructions": "Mix and cook." }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("JSON-LD Wins", result.Name);
    }

    [Fact]
    public async Task ParseAsync_Microdata()
    {
        const string html = """
            <html><body>
            <div itemscope itemtype="https://schema.org/Recipe">
              <span itemprop="name">Microdata Pancakes</span>
              <span itemprop="recipeIngredient">1 cup flour</span>
              <span itemprop="recipeIngredient">2 eggs</span>
              <span itemprop="recipeInstructions">Mix everything.</span>
              <span itemprop="recipeInstructions">Cook on a hot pan.</span>
            </div>
            </body></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Microdata Pancakes", result.Name);
        Assert.Equal(ExpectedInstructions, result.Recipe);
        Assert.Equal(["1 cup flour", "2 eggs"], result.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public async Task ParseAsync_Microdata_IgnoresNestedItemScopeProperties()
    {
        const string html = """
            <html><body>
            <div itemscope itemtype="https://schema.org/Recipe">
              <span itemprop="name">Microdata Pancakes</span>
              <div itemprop="author" itemscope itemtype="https://schema.org/Person">
                <span itemprop="name">Someone Else</span>
              </div>
            </div>
            </body></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Microdata Pancakes", result.Name);
    }

    [Fact]
    public async Task ParseAsync_OpenGraphFallback()
    {
        const string html = """
            <html><head>
            <meta property="og:title" content="OG Only Recipe" />
            <meta property="og:description" content="A tasty recipe description." />
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("OG Only Recipe", result.Name);
        Assert.Equal("A tasty recipe description.", result.Recipe);
        Assert.Empty(result.Ingredients);
    }

    [Fact]
    public async Task ParseAsync_NothingFound_ReturnsNull()
    {
        const string html = "<html><head><title>Just a page</title></head><body><p>No recipe here.</p></body></html>";

        var result = await _parser.ParseAsync(html);

        Assert.Null(result);
    }
}
