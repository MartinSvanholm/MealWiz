using Anthropic;
using TheMealWorkshop.Functions.Functions.AnalyzeMealPicture;
using TheMealWorkshop.Functions.Functions.CleanUpMealPictures;
using TheMealWorkshop.Functions.Functions.DeleteAccount;
using TheMealWorkshop.Functions.Functions.ScrapeRecipe;
using TheMealWorkshop.Functions.Functions.ScrapeRecipe.Parsing;
using TheMealWorkshop.Functions.Shared.Fetching;
using TheMealWorkshop.Functions.Shared.Ingredients;
using TheMealWorkshop.Functions.Shared.Ssrf;
using TheMealWorkshop.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TheMealWorkshop.Functions;

/// <summary>
/// The app's service registrations, kept out of Program.cs so a test can build the same container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTheMealWorkshopFunctions(this IServiceCollection services)
    {
        services.AddSingleton<IIpAddressGuard, IpAddressGuard>();
        services.AddSingleton<ISsrfGuardedHttpClientFactory, SsrfGuardedHttpClientFactory>();
        services.AddSingleton<IRecipeFetcher, RecipeFetcher>();

        services.AddSingleton<IJsonLdRecipeParser, JsonLdRecipeParser>();
        services.AddSingleton<IMicrodataRecipeParser, MicrodataRecipeParser>();
        services.AddSingleton<IOpenGraphRecipeParser, OpenGraphRecipeParser>();
        services.AddSingleton<IRecipeParser, RecipeParser>();
        services.AddSingleton<IIngredientLineParser, IngredientLineParser>();
        services.AddSingleton<IRecipeMapper, RecipeMapper>();

        services.AddSingleton<IServiceRoleSupabaseClientFactory, ServiceRoleSupabaseClientFactory>();
        services.AddScoped<IScrapeRecipeHandler, ScrapeRecipeHandler>();

        // The picture analysis. The SDK makes its own HttpClient; FN-02 is about user-supplied URLs, and this one only
        // calls the Anthropic API. One attempt may take 40 s and there is one retry; the analyzer caps the whole call at 80 s.
        services.AddSingleton<IAnthropicClient>(sp => new AnthropicClient
        {
            ApiKey = sp.GetRequiredService<IConfiguration>()["Anthropic:ApiKey"],
            MaxRetries = 1,
            Timeout = TimeSpan.FromSeconds(40)
        });
        services.AddSingleton<IMealPictureAnalyzer, AnthropicMealPictureAnalyzer>();
        services.AddScoped<IAnalyzeMealPictureHandler, AnalyzeMealPictureHandler>();
        services.AddScoped<ICleanUpMealPicturesHandler, CleanUpMealPicturesHandler>();

        services.AddHttpClient(DeleteAccountHandler.HttpClientName);
        services.AddScoped<IDeleteAccountHandler, DeleteAccountHandler>();

        return services;
    }
}
