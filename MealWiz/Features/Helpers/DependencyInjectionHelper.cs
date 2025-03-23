using Features.Services.DrawerStateContainer;
using MealWizFeatures.Features.Meals.State;
using MealWizFeatures.Services.DrawerStateContainer;
using Microsoft.Extensions.DependencyInjection;

namespace MealWizFeatures.Helpers;

public static class DependencyInjectionHelper
{
    public static void RegisterScopedServices(this IServiceCollection services)
    {
        services.AddScoped<IMealsStateContainer, MealsStateContainer>();
        services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
    }
}