using Anthropic;
using MealWiz.Functions.Functions.AnalyzeMealPicture;
using MealWiz.Functions.Functions.CleanUpMealPictures;
using MealWiz.Functions.Functions.DeleteAccount;
using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.Ingredients;
using MealWiz.Functions.Shared.Ssrf;
using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MealWiz.Functions;

/// <summary>
/// The app's service registrations, kept out of Program.cs so a test can build the same container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMealWizFunctions(this IServiceCollection services)
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
