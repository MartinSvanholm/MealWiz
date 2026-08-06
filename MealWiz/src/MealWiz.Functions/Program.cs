using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Fetching;
using MealWiz.Functions.Shared.Ssrf;
using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IIpAddressGuard, IpAddressGuard>();
        services.AddSingleton<ISsrfGuardedHttpClientFactory, SsrfGuardedHttpClientFactory>();
        services.AddSingleton<IRecipeFetcher, RecipeFetcher>();

        services.AddSingleton<IJsonLdRecipeParser, JsonLdRecipeParser>();
        services.AddSingleton<IMicrodataRecipeParser, MicrodataRecipeParser>();
        services.AddSingleton<IOpenGraphRecipeParser, OpenGraphRecipeParser>();
        services.AddSingleton<IRecipeParser, RecipeParser>();
        services.AddSingleton<IRecipeMapper, RecipeMapper>();

        services.AddSingleton<IServiceRoleSupabaseClientFactory, ServiceRoleSupabaseClientFactory>();
        services.AddScoped<ScrapeRecipeHandler>();
    })
    .Build();

host.Run();
