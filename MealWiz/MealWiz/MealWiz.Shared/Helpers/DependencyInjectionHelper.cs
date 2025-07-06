using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Services.DrawerStateContainer;
using Microsoft.Extensions.DependencyInjection;

namespace MealWiz.Shared.Helpers;

public static class DependencyInjectionHelper
{
    public static void RegisterScopedServices(this IServiceCollection services)
    {
        services.AddScoped<IMealsStateContainer, MealsStateContainer>();
        services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
    }
}