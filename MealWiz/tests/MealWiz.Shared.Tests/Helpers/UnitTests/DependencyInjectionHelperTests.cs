using MealWiz.Shared.Features.GroceryList.State;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Features.MealPlans.State;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Services.DrawerStateContainer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DrawerStateContainerService = MealWiz.Shared.Services.DrawerStateContainer.DrawerStateContainer;

namespace MealWiz.Shared.Tests.Helpers.UnitTests;

public class DependencyInjectionHelperTests
{
    [Theory]
    [InlineData(typeof(IMealsStateContainer), typeof(MealsStateContainer))]
    [InlineData(typeof(IDrawerStateContainer), typeof(DrawerStateContainerService))]
    [InlineData(typeof(IMealPlanStateContainer), typeof(MealPlanStateContainer))]
    [InlineData(typeof(IGroceryListStateContainer), typeof(GroceryListStateContainer))]
    [InlineData(typeof(IMealImportRealtimeSubscriber), typeof(MealImportRealtimeSubscriber))]
    [InlineData(typeof(IMealImportStateContainer), typeof(MealImportStateContainer))]
    public void RegisterScopedServices_RegistersEachStateContainerAsScoped(Type service, Type implementation)
    {
        var services = new ServiceCollection();

        services.RegisterScopedServices();

        var descriptor = Assert.Single(services, d => d.ServiceType == service);
        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
