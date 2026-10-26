using Blazored.LocalStorage;
using Bunit;
using Bunit.TestDoubles;
using FluentResults;
using MealWiz.Shared.Components.Drawers;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.State;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.State;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Services.DrawerStateContainer;
using MealWiz.Shared.Theme;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace MealWiz.Shared.Tests.TestInfrastructure;

/// <summary>
/// bUnit context for MealWiz.Shared components. MediatR is mocked; the Meals, GroceryList, MealPlan and Drawer
/// state containers are the real in-memory ones running on that mock, so a test can assert the actual request a
/// click ends in. The MealImport container is mocked (it drives Realtime and timers; its own unit tests cover it).
/// Elements are found by <c>data-testid</c>, never by MudBlazor markup.
/// </summary>
public abstract class ComponentTestBase : BunitContext
{
    protected Mock<IMediator> Mediator { get; } = new();
    protected Mock<ILocalStorageService> LocalStorage { get; } = new();
    protected Mock<IMealImportStateContainer> MealImport { get; } = new();
    protected Mock<IThemeService> Theme { get; } = new();

    protected DrawerStateContainer Drawer { get; } = new();
    protected MealsStateContainer Meals { get; }
    protected GroceryListStateContainer GroceryList { get; }
    protected MealPlanStateContainer MealPlan { get; }

    /// <summary>bUnit's fake authentication; see <see cref="AuthTestHelper"/>.</summary>
    protected BunitAuthorizationContext Auth { get; }

    protected ComponentTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // The 1s default is too tight on a busy machine or CI runner (the first MudMenu popover render is slow
        // while cold); waits still return as soon as their condition holds.
        DefaultWaitTimeout = TimeSpan.FromSeconds(5);

        Services.AddMudServices();

        Meals = new MealsStateContainer(Mediator.Object);
        GroceryList = new GroceryListStateContainer(Mediator.Object);
        MealPlan = new MealPlanStateContainer(Mediator.Object, LocalStorage.Object);

        Services.AddSingleton(Mediator.Object);
        Services.AddSingleton<ISender>(Mediator.Object);
        Services.AddSingleton(LocalStorage.Object);
        Services.AddSingleton(MealImport.Object);
        Services.AddSingleton(Theme.Object);
        Services.AddSingleton<IDrawerStateContainer>(Drawer);
        Services.AddSingleton<IMealsStateContainer>(Meals);
        Services.AddSingleton<IGroceryListStateContainer>(GroceryList);
        Services.AddSingleton<IMealPlanStateContainer>(MealPlan);
        Services.AddSingleton(_ => new Supabase.Client("http://localhost", "test-key"));

        // Registered up front because bUnit allows no registrations once a service has been resolved (below).
        // Anonymous until a test calls Auth.SetAuthorized(...).
        Auth = AddAuthorization();

        // Pages set CurrentSnackbar themselves; leaf components (buttons, drawers) rely on a page having done it.
        Meals.CurrentSnackbar = Snackbar;
        GroceryList.CurrentSnackbar = Snackbar;
        MealPlan.CurrentSnackbar = Snackbar;

        // Empty-but-successful defaults for the loads that pages and containers fire on init.
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal>()));
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>()));
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(null));
        MealImport.SetupGet(m => m.JobStatus).Returns(MealImportJobStatus.Idle);
        MealImport.SetupGet(m => m.EntryState).Returns(MealImportEntryState.Default);
    }

    /// <summary>CSS selector for a <c>data-testid</c>, e.g. <c>cut.Find(TestId("save-meal-button"))</c>.</summary>
    protected static string TestId(string id) => $"[data-testid=\"{id}\"]";

    /// <summary>
    /// The text input of a field with that <c>data-testid</c>, whether the attribute ends up on the input itself
    /// (MudTextField) or on a wrapper around it.
    /// </summary>
    protected static string InputOf(string id) => $"input{TestId(id)}, textarea{TestId(id)}, {TestId(id)} input, {TestId(id)} textarea";

    protected ISnackbar Snackbar =>Services.GetRequiredService<ISnackbar>();

    protected IEnumerable<string> SnackbarMessages => Snackbar.ShownSnackbars.Select(s => s.Message);

    protected BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    /// <summary>Path of the current URI relative to the base, e.g. "meals/edit" (empty for the root).</summary>
    protected string CurrentPath => Navigation.ToBaseRelativePath(Navigation.Uri);

    protected void SetupSend<TRequest, TResponse>(TResponse response) where TRequest : IRequest<TResponse> =>
        Mediator.Setup(m => m.Send(It.IsAny<TRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

    /// <summary>Every request sent through the mocked mediator, in order.</summary>
    protected IEnumerable<object> SentRequests =>
        Mediator.Invocations.Where(i => i.Method.Name == nameof(IMediator.Send)).Select(i => i.Arguments[0]);

    /// <summary>The requests of type <typeparamref name="TRequest"/> sent through the mocked mediator.</summary>
    protected List<TRequest> Sent<TRequest>() => SentRequests.OfType<TRequest>().ToList();

    /// <summary>
    /// Renders a MudPopoverProvider before <typeparamref name="TComponent"/>. Menus and tooltips open into the
    /// provider, so look for their content in <see cref="Popovers"/>, not in the returned component.
    /// </summary>
    protected IRenderedComponent<TComponent> RenderWithPopovers<TComponent>(
        Action<ComponentParameterCollectionBuilder<TComponent>>? parameters = null) where TComponent : IComponent
    {
        Popovers = Render<MudPopoverProvider>();
        return Render(parameters ?? (_ => { }));
    }

    protected IRenderedComponent<MudPopoverProvider> Popovers { get; private set; } = default!;

    /// <summary>
    /// Renders the app's two drawers (as MainLayout does) so a test can drive a whole flow, e.g. click a button,
    /// fill in the drawer it opens and confirm. Look for drawer content in <see cref="DrawerHost"/> and
    /// <see cref="ConfirmDrawerHost"/>.
    /// </summary>
    protected void RenderDrawers()
    {
        DrawerHost = Render<DrawerBase>();
        ConfirmDrawerHost = Render<ConfirmDrawer>();
    }

    protected IRenderedComponent<DrawerBase> DrawerHost { get; private set; } = default!;
    protected IRenderedComponent<ConfirmDrawer> ConfirmDrawerHost { get; private set; } = default!;

    /// <summary>Clicks Confirm in the confirm drawer.</summary>
    protected void Confirm() => ConfirmDrawerHost.Find(TestId("confirm-drawer-confirm")).Click();
}
