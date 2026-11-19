using TheMealWorkshop.Shared.Features.GroceryCategories.State;
using TheMealWorkshop.Shared.Features.GroceryList.State;
using TheMealWorkshop.Shared.Features.MealImport.State;
using TheMealWorkshop.Shared.Features.MealPlans.State;
using TheMealWorkshop.Shared.Features.Meals.State;
using TheMealWorkshop.Shared.Helpers;
using TheMealWorkshop.Shared.Services.DrawerStateContainer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DrawerStateContainerService = TheMealWorkshop.Shared.Services.DrawerStateContainer.DrawerStateContainer;

namespace TheMealWorkshop.Shared.Tests.Helpers.UnitTests;

public class DependencyInjectionHelperTests
{
    [Theory]
    [InlineData(typeof(IMealsStateContainer), typeof(MealsStateContainer))]
    [InlineData(typeof(IDrawerStateContainer), typeof(DrawerStateContainerService))]
    [InlineData(typeof(IMealPlanStateContainer), typeof(MealPlanStateContainer))]
    [InlineData(typeof(IPlanWeekStateContainer), typeof(PlanWeekStateContainer))]
    [InlineData(typeof(IGroceryListStateContainer), typeof(GroceryListStateContainer))]
    [InlineData(typeof(IGroceryCategoriesStateContainer), typeof(GroceryCategoriesStateContainer))]
    [InlineData(typeof(IMealImportStateContainer), typeof(MealImportStateContainer))]
    public void RegisterScopedServices_RegistersEachStateContainerAsScoped(Type service, Type implementation)
    {
        var services = new ServiceCollection();

        services.RegisterScopedServices();

        var descriptor = Assert.Single(services, d => d.ServiceType == service);
        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterScopedServices_HttpClientOutwaitsTheFunctionAppsTwoMinuteLimit()
    {
        var services = new ServiceCollection();
        services.RegisterScopedServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

        // host.json of TheMealWorkshop.Functions: "functionTimeout": "00:02:00".
        Assert.True(httpClient.Timeout > TimeSpan.FromMinutes(2));
        Assert.Equal(DependencyInjectionHelper.HttpClientTimeout, httpClient.Timeout);
    }
}
