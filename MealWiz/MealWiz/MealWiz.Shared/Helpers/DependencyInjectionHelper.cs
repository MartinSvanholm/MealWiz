using MealWiz.Shared.Features.GroceryList.State;
using MealWiz.Shared.Features.MealPlans.State;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Localization;
using MealWiz.Shared.Services.DrawerStateContainer;
using MealWiz.Shared.Theme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;

namespace MealWiz.Shared.Helpers;

public static class DependencyInjectionHelper
{
    public static void RegisterScopedServices(this IServiceCollection services)
    {
        services.AddScoped<IMealsStateContainer, MealsStateContainer>();
        services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
        services.AddScoped<IMealPlanStateContainer, MealPlanStateContainer>();
        services.AddScoped<IPlanWeekStateContainer, PlanWeekStateContainer>();
        services.AddScoped<IGroceryListStateContainer, GroceryListStateContainer>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<MealWiz.Shared.Features.Authentication.EmailLinks.EmailLinkBuilder>(); // each head registers its EmailLinkOptions
        services.AddScoped<IMealImportRealtimeSubscriber, MealImportRealtimeSubscriber>();
        services.AddScoped<IMealImportStateContainer, MealImportStateContainer>();

        // IStringLocalizer<T> over the .resx resources (no ResourcesPath: resources sit beside their marker type),
        // and MudBlazor's built-in texts from the shared resource. Replace, so the order against AddMudServices
        // doesn't matter.
        services.AddLocalization();
        services.Replace(ServiceDescriptor.Transient<MudLocalizer, MealWizMudLocalizer>());
    }
}