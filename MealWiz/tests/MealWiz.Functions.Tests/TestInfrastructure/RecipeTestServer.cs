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
    // Danish on purpose: real imports are Danish, so this exercises æøå and ½ through the whole path
    // (page encoding -> fetch -> parse -> ingredient split -> Supabase jsonb -> read back).
    public const string RecipeName = "Æblepandekager";
    public const string RecipeInstructions = "Pisk det hele til en jævn dej.\nSteg pandekagerne i 1 min på hver side.";

    private const string RecipePageHtml = """
        <html><head>
        <meta charset="utf-8">
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Æblepandekager",
          "recipeIngredient": [
            "2½ dl sødmælk, stuetempereret",
            "en knivspids salt",
            "1 (400 g) dåse hakkede tomater",
            "salt og peber"
          ],
          "recipeInstructions": [
            { "@type": "HowToStep", "text": "Pisk det hele til en jævn dej." },
            { "@type": "HowToStep", "text": "Steg pandekagerne i 1 min på hver side." }
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
