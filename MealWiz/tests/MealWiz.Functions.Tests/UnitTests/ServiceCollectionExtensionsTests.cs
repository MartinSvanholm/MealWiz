using MealWiz.Functions.Functions.ScrapeRecipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MealWiz.Functions.Tests.UnitTests;

/// <summary>
/// Builds the container the way Program.cs does, so a missing or mis-scoped registration fails here
/// instead of on the first request in Azure.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Host_BuildsAndCreatesScrapeRecipeFunction()
    {
        using var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(services => services.AddMealWizFunctions())
            // Validates every registration up front, including singletons that capture a scoped service.
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .Build();

        using var scope = host.Services.CreateScope();

        // The worker creates function classes per invocation from the scoped provider, without registering them.
        var function = ActivatorUtilities.CreateInstance<ScrapeRecipeFunction>(scope.ServiceProvider);

        Assert.NotNull(function);
    }
}
