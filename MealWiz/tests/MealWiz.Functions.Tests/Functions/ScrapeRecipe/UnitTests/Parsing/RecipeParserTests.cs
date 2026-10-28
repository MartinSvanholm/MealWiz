using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.ScrapeRecipe.UnitTests.Parsing;

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
    public async Task ParseAsync_JsonLd_HowToSectionInstructions()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@type": "Recipe",
              "name": "Sectioned Pancakes",
              "recipeInstructions": [
                {
                  "@type": "HowToSection",
                  "name": "Batter",
                  "itemListElement": [
                    {"@type": "HowToStep", "text": "Whisk the eggs."},
                    {"@type": "HowToStep", "text": "Mix in the flour."}
                  ]
                },
                {
                  "@type": "HowToSection",
                  "name": "Cooking",
                  "itemListElement": [{"@type": "HowToStep", "text": "Cook on a hot pan."}]
                },
                {"@type": "HowToStep", "text": "Serve."}
              ]
            }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Whisk the eggs.\nMix in the flour.\nCook on a hot pan.\nServe.", result.Recipe);
    }

    // Many WordPress recipe plugins HTML-encode their JSON-LD strings. Microdata and OpenGraph values are
    // decoded by the HTML parser, so JSON-LD is decoded too to give the same text whichever tier matched.
    [Fact]
    public async Task ParseAsync_JsonLd_DecodesHtmlEntities()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@type": "Recipe",
              "name": "Mac &amp; Cheese",
              "recipeIngredient": ["1 &frac12; cup milk"],
              "recipeInstructions": [{"@type": "HowToStep", "text": "Stir until &quot;creamy&quot;."}]
            }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Mac & Cheese", result.Name);
        Assert.Equal(["1 ½ cup milk"], result.Ingredients.Select(i => i.Name));
        Assert.Equal("Stir until \"creamy\".", result.Recipe);
    }

    [Fact]
    public async Task ParseAsync_JsonLd_StringInstructionsAreDecoded()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "Toast", "recipeInstructions": "Toast &amp; butter." }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Toast & butter.", result.Recipe);
    }

    [Fact]
    public async Task ParseAsync_JsonLd_MalformedBlockIsSkipped()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">{ "@type": "Recipe", "name": "Broken", </script>
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "Second Block Pancakes", "recipeInstructions": "Mix and cook." }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Second Block Pancakes", result.Name);
    }

    [Fact]
    public async Task ParseAsync_JsonLd_TypeArray()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            { "@type": ["Recipe", "NewsArticle"], "name": "Multi-Typed Pancakes", "recipeInstructions": "Mix and cook." }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Multi-Typed Pancakes", result.Name);
    }

    [Fact]
    public async Task ParseAsync_JsonLd_LegacyIngredientsProperty()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "Legacy Pancakes", "ingredients": ["1 cup flour", "2 eggs"] }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal(["1 cup flour", "2 eggs"], result.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public async Task ParseAsync_JsonLd_IgnoresNonStringIngredients()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@type": "Recipe",
              "name": "Messy Pancakes",
              "recipeIngredient": ["1 cup flour", 2, null, {"name": "eggs"}, ["milk"], "  ", "salt"]
            }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal(["1 cup flour", "salt"], result.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public async Task ParseAsync_JsonLdWithoutName_FallsThroughToMicrodata()
    {
        const string html = """
            <html><head>
            <meta property="og:title" content="OG Title Should Be Ignored" />
            <script type="application/ld+json">
            { "@type": "Recipe", "recipeIngredient": ["1 cup flour"] }
            </script>
            </head><body>
            <div itemscope itemtype="https://schema.org/Recipe">
              <span itemprop="name">Microdata Pancakes</span>
            </div>
            </body></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Microdata Pancakes", result.Name);
    }

    [Fact]
    public async Task ParseAsync_JsonLdWithoutName_FallsThroughToOpenGraph()
    {
        const string html = """
            <html><head>
            <meta property="og:title" content="OG Only Recipe" />
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "  ", "recipeIngredient": ["1 cup flour"] }
            </script>
            </head></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("OG Only Recipe", result.Name);
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
    public async Task ParseAsync_Microdata_MetaContentValues()
    {
        const string html = """
            <html><body>
            <div itemscope itemtype="https://schema.org/Recipe">
              <meta itemprop="name" content="Meta Pancakes" />
              <meta itemprop="recipeIngredient" content="1 cup flour" />
              <span itemprop="recipeIngredient">2 eggs</span>
              <meta itemprop="recipeInstructions" content="Mix everything." />
              <meta itemprop="recipeInstructions" content="Cook on a hot pan." />
            </div>
            </body></html>
            """;

        var result = await _parser.ParseAsync(html);

        Assert.NotNull(result);
        Assert.Equal("Meta Pancakes", result.Name);
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
