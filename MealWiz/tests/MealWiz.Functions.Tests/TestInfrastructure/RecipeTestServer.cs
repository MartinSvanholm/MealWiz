using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MealWiz.Functions.Tests.TestInfrastructure;

/// <summary>
/// Local stand-in for the live websites used in manual smoke testing, so integration tests don't
/// depend on the internet or on a third-party site's markup staying the same. Listens on a random
/// loopback port; tests reach it through <see cref="LoopbackAllowingIpAddressGuard"/>.
/// </summary>
public sealed class RecipeTestServer : IAsyncLifetime
{
    public const string RecipeName = "Test Pancakes";
    public const string RecipeInstructions = "Whisk everything into a smooth batter.\nCook for 1 min on each side.";
    public static readonly string[] RecipeIngredients = ["100g plain flour", "2 large eggs", "300ml milk"];

    private const string RecipePageHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Test Pancakes",
          "recipeIngredient": ["100g plain flour", "2 large eggs", "300ml milk"],
          "recipeInstructions": [
            { "@type": "HowToStep", "text": "Whisk everything into a smooth batter." },
            { "@type": "HowToStep", "text": "Cook for 1 min on each side." }
          ]
        }
        </script>
        </head><body></body></html>
        """;

    private const string NoRecipePageHtml =
        "<html><head><title>Not a recipe</title></head><body><p>Nothing to see here.</p></body></html>";

    // Comfortably over RecipeFetcher's 5 MB cap.
    private const int OversizedResponseBytes = 6 * 1024 * 1024;

    private WebApplication? _app;

    public Uri BaseUri { get; private set; } = null!;

    public string Url(string path) => new Uri(BaseUri, path).ToString();

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();

        _app.MapGet("/recipe", () => Results.Content(RecipePageHtml, "text/html"));
        _app.MapGet("/no-recipe", () => Results.Content(NoRecipePageHtml, "text/html"));
        _app.MapGet("/missing", () => Results.NotFound());

        _app.MapGet("/redirect/relative", () => Results.Redirect("/recipe"));
        _app.MapGet("/redirect/metadata", () => Results.Redirect("http://169.254.169.254/latest/meta-data"));
        _app.MapGet("/redirect/ftp", () => Results.Redirect("ftp://example.com/recipe"));

        // /redirect/chain/{n} issues exactly n redirects before serving the recipe page.
        _app.MapGet("/redirect/chain/{hops:int}", (int hops) => hops > 0
            ? Results.Redirect($"/redirect/chain/{hops - 1}")
            : Results.Content(RecipePageHtml, "text/html"));

        // Chunked, with no Content-Length, so the fetcher can't rely on the header to enforce its cap.
        _app.MapGet("/oversized", async (HttpContext context) =>
        {
            context.Response.ContentType = "text/html";
            var chunk = new byte[64 * 1024];
            Array.Fill(chunk, (byte)'a');

            try
            {
                for (var written = 0; written < OversizedResponseBytes; written += chunk.Length)
                {
                    await context.Response.Body.WriteAsync(chunk, context.RequestAborted);
                }
            }
            catch (Exception) when (context.RequestAborted.IsCancellationRequested)
            {
                // Expected: the fetcher hangs up once it passes the cap.
            }
        });

        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>()
            .Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.First();

        BaseUri = new Uri(address);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null) return;

        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
