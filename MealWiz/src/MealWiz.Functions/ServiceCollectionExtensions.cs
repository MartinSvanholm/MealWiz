using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.Ingredients;
using MealWiz.Functions.Shared.Ssrf;
using MealWiz.Functions.Shared.SupabaseAccess;
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

        return services;
    }
}
