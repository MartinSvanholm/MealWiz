using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace MauiWASM.Tests.TestInfrastructure.E2E;

/// <summary>
/// Serves the published wwwroot on a random loopback port, the way Azure Static Web Apps serves it in production:
/// static files plus a fallback to index.html for client-side routes. Two differences, both so the app talks to the
/// local stack:
/// <list type="bullet">
/// <item>Every response carries <c>Blazor-Environment: Development</c>. Without it a published app boots as
/// Production.</item>
/// <item><c>appsettings.Development.json</c> is answered from memory with the local stack's URL and key, and a
/// same-origin <see cref="FunctionsPath"/> for the meal import Function. The boot manifest only loads the config
/// files it lists, so overriding this one is the hook.</item>
/// </list>
/// </summary>
public sealed class E2EAppHost : IAsyncDisposable
{
    /// <summary>
    /// Where the app sends <c>POST {FunctionsPath}/api/ScrapeRecipe</c>. Same origin as the app, so no CORS. Tests
    /// that import a meal intercept it in the browser; anything that reaches the server gets a 503.
    /// </summary>
    public const string FunctionsPath = "/functions";

    private readonly WebApplication _app;

    private E2EAppHost(WebApplication app, Uri baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    /// <summary>The app's root, with a trailing slash.</summary>
    public Uri BaseUrl { get; }

    public static async Task<E2EAppHost> StartAsync(string wwwroot, string supabaseUrl, string supabaseKey)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = wwwroot,
            WebRootPath = wwwroot
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        var files = new PhysicalFileProvider(wwwroot);

        app.Use((context, next) =>
        {
            context.Response.Headers["Blazor-Environment"] = "Development";
            // Every test gets a fresh browser context, but keep a re-published app from being served stale.
            context.Response.Headers.CacheControl = "no-store";
            return next(context);
        });

        // Endpoints win over the static file middleware, so this replaces the published file.
        app.MapGet("/appsettings.Development.json", (HttpContext context) =>
        {
            var origin = $"{context.Request.Scheme}://{context.Request.Host}";
            var settings = new
            {
                Supabase = new { url = supabaseUrl, key = supabaseKey },
                MealImportFunction = new { url = origin + FunctionsPath, key = "" }
            };
            return Results.Text(JsonSerializer.Serialize(settings), "application/json");
        });

        app.MapPost(FunctionsPath + "/api/ScrapeRecipe", () => Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".wasm"] = "application/wasm";
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        contentTypes.Mappings[".blat"] = "application/octet-stream";
        contentTypes.Mappings[".dll"] = "application/octet-stream";

        app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ContentTypeProvider = contentTypes });
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });

        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.Single();

        return new E2EAppHost(app, new Uri(address.TrimEnd('/') + "/"));
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
