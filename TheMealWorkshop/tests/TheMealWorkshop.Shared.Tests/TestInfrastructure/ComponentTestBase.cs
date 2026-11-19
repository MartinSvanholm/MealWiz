using Blazored.LocalStorage;
using Bunit;
using Bunit.TestDoubles;
using FluentResults;
using TheMealWorkshop.Shared.Components.Drawers;
using TheMealWorkshop.Shared.Features.Account.GetPendingEmailChange;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryListSettings;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetRememberedGroceryNames;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Features.GroceryCategories.State;
using TheMealWorkshop.Shared.Features.GroceryList.DeleteGroceryItemSources;
using TheMealWorkshop.Shared.Features.GroceryList.GetAllGroceryItems;
using TheMealWorkshop.Shared.Features.GroceryList.GetGroceryItemSources;
using TheMealWorkshop.Shared.Features.GroceryList.Models;
using TheMealWorkshop.Shared.Features.GroceryList.State;
using TheMealWorkshop.Shared.Features.MealImport.Models;
using TheMealWorkshop.Shared.Features.MealImport.State;
using TheMealWorkshop.Shared.Services.ScreenWakeLock;
using TheMealWorkshop.Shared.Features.MealPlans.GetMealPlanByDate;
using TheMealWorkshop.Shared.Features.MealPlans.GetMealPlannedDates;
using TheMealWorkshop.Shared.Features.MealPlans.Models;
using TheMealWorkshop.Shared.Features.MealPlans.State;
using TheMealWorkshop.Shared.Features.Meals.GetAllMeals;
using TheMealWorkshop.Shared.Features.Meals.Models;
using TheMealWorkshop.Shared.Features.Meals.State;
using TheMealWorkshop.Shared.Localization;
using TheMealWorkshop.Shared.Services.Authentication;
using TheMealWorkshop.Shared.Services.DrawerStateContainer;
using TheMealWorkshop.Shared.Theme;
using MediatR;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.TestInfrastructure;

/// <summary>
/// bUnit context for TheMealWorkshop.Shared components. MediatR is mocked; the Meals, GroceryList, GroceryCategories, MealPlan
/// and Drawer state containers are the real in-memory ones running on that mock, so a test can assert the actual request a
/// click ends in. The MealImport container is mocked (it drives timers and polling; its own unit tests cover it).
/// Elements are found by <c>data-testid</c>, never by MudBlazor markup.
/// </summary>
public abstract class ComponentTestBase : BunitContext
{
    protected Mock<IMediator> Mediator { get; } = new();
    protected Mock<ILocalStorageService> LocalStorage { get; } = new();
    protected Mock<IMealImportStateContainer> MealImport { get; } = new();
    protected Mock<IMealPictureShrinker> PictureShrinker { get; } = new();
    protected Mock<IScreenWakeLock> WakeLock { get; } = new();
    protected Mock<IThemeService> Theme { get; } = new();
    protected Mock<ILanguagePreference> Language { get; } = new();

    /// <summary>The session keeper the page resolved; counts <see cref="SessionKeeper.RefreshNowAsync"/> calls.</summary>
    protected RecordingSessionKeeper Keeper { get; private set; } = default!;

    /// <summary>The email link options the builder is created with (the web head's unless a test sets them).</summary>
    protected TheMealWorkshop.Shared.Features.Authentication.EmailLinks.EmailLinkOptions EmailLinks { get; set; } = new(null);

    protected DrawerStateContainer Drawer { get; } = new();
    protected MealsStateContainer Meals { get; }
    protected GroceryListStateContainer GroceryList { get; }
    protected GroceryCategoriesStateContainer GroceryCategories { get; }
    protected MealPlanStateContainer MealPlan { get; }

    /// <summary>The real Plan week container, on the mocked MediatR and <see cref="Time"/>, with a seeded Random.</summary>
    protected PlanWeekStateContainer PlanWeek => (PlanWeekStateContainer)Services.GetRequiredService<IPlanWeekStateContainer>();

    /// <summary>The clock: Wednesday 2026-11-04 12:00 UTC, so this week starts Monday 2026-11-02.</summary>
    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    /// <summary>bUnit's fake authentication; see <see cref="AuthTestHelper"/>.</summary>
    protected BunitAuthorizationContext Auth { get; }

    protected ComponentTestBase() : this(echoResourceKeys: false)
    {
    }

    /// <param name="echoResourceKeys">
    /// Every <c>IStringLocalizer</c> returns <c>[Key]</c> instead of the resource text, so a test can prove a text
    /// comes from a resource and isn't hard-coded (<see cref="EchoStringLocalizerFactory"/>).
    /// </param>
    protected ComponentTestBase(bool echoResourceKeys)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // The 1s default is too tight on a busy machine or CI runner (the first MudMenu popover render is slow
        // while cold); waits still return as soon as their condition holds.
        DefaultWaitTimeout = TimeSpan.FromSeconds(5);

        Services.AddMudServices();
        // The real .resx resources, as the hosts register them; the run is pinned to en-US (xunit.runner.json).
        Services.AddLocalization();
        Services.Replace(ServiceDescriptor.Transient<MudLocalizer, TheMealWorkshopMudLocalizer>());
        if (echoResourceKeys)
            Services.AddSingleton<IStringLocalizerFactory, EchoStringLocalizerFactory>();

        Meals = new MealsStateContainer(Mediator.Object);
        GroceryList = new GroceryListStateContainer(Mediator.Object, Time);
        GroceryCategories = new GroceryCategoriesStateContainer(Mediator.Object, LocalStorage.Object, Time);
        MealPlan = new MealPlanStateContainer(Mediator.Object, LocalStorage.Object);

        Services.AddSingleton(Mediator.Object);
        Services.AddSingleton<ISender>(Mediator.Object);
        Services.AddSingleton(LocalStorage.Object);
        Services.AddSingleton(MealImport.Object);
        Services.AddSingleton(PictureShrinker.Object);
        Services.AddSingleton(WakeLock.Object);
        Services.AddSingleton(Theme.Object);
        Services.AddSingleton(Language.Object);
        Services.AddSingleton<IDrawerStateContainer>(Drawer);
        Services.AddSingleton<IMealsStateContainer>(Meals);
        Services.AddSingleton<IGroceryListStateContainer>(GroceryList);
        Services.AddSingleton<IGroceryCategoriesStateContainer>(GroceryCategories);
        Services.AddSingleton<IMealPlanStateContainer>(MealPlan);
        Time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<IPlanWeekStateContainer>(sp =>
            new PlanWeekStateContainer(Mediator.Object, LocalStorage.Object, Time, MealPlan, sp.GetRequiredService<NavigationManager>())
            {
                Random = new Random(1)
            });
        Services.AddSingleton(_ => new Supabase.Client("http://localhost", "test-key"));
        // The web head's link options; a test of the MAUI head sets EmailLinks before rendering.
        Services.AddSingleton(_ => EmailLinks);
        Services.AddScoped<TheMealWorkshop.Shared.Features.Authentication.EmailLinks.EmailLinkBuilder>();
        // The startup gate in Routes starts it; with no stored session it is done at once.
        Services.AddSingleton<SessionKeeper>(sp => Keeper = new RecordingSessionKeeper(sp.GetRequiredService<Supabase.Client>(), new EmptySessionPersistence(), Time));

        // Registered up front because bUnit allows no registrations once a service has been resolved (below).
        // Anonymous until a test calls Auth.SetAuthorized(...).
        Auth = AddAuthorization();

        // Pages set CurrentSnackbar themselves; leaf components (buttons, drawers) rely on a page having done it.
        Meals.CurrentSnackbar = Snackbar;
        GroceryList.CurrentSnackbar = Snackbar;
        GroceryCategories.CurrentSnackbar = Snackbar;
        MealPlan.CurrentSnackbar = Snackbar;

        // Empty-but-successful defaults for the loads that pages and containers fire on init.
        SetupSend<GetPendingEmailChange.Query, Result<string?>>(Result.Ok<string?>(null));
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal>()));
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>()));
        SetupSend<GetGroceryCategories.Query, Result<List<GroceryCategory>>>(Result.Ok(TestData.DefaultGroceryCategories()));
        SetupSend<GetRememberedGroceryNames.Query, Result<List<RememberedGroceryName>>>(Result.Ok(new List<RememberedGroceryName>()));
        SetupSend<GetGroceryListSettings.Query, Result<GroceryListSettings>>(Result.Ok(new GroceryListSettings()));
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(new List<GroceryItemSource>()));
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Ok<MealPlan?>(null));
        SetupSend<GetMealPlannedDates.Query, Result<List<PlannedMealDate>>>(Result.Ok(new List<PlannedMealDate>()));
        SetupSend<DeleteGroceryItemSources.Command, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>()));
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

    /// <summary>Presses Enter in a field, as the Enter (or Next) key of a phone keyboard does.</summary>
    protected static void PressEnter(IElement input) => input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

    /// <summary>Asserts that <paramref name="input"/> is the element most recently focused through FocusAsync.</summary>
    protected void AssertFocused(IElement input)
    {
        var focused = (ElementReference)JSInterop.Invocations["Blazor._internal.domWrapper.focus"].Last().Arguments[0]!;
        Assert.Equal(focused.Id, input.GetAttribute("blazor:elementReference"));
    }

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

    /// <summary>
    /// <see cref="SentRequests"/> without the category loads the category chips start (a drawer with chips on a page
    /// that hasn't loaded them, such as Edit meal), for tests that check nothing is saved.
    /// </summary>
    protected IEnumerable<object> SentRequestsBesidesCategoryLoads =>
        SentRequests.Where(r => r is not GetGroceryCategories.Query and not GetRememberedGroceryNames.Query and not GetGroceryListSettings.Query);

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

internal sealed class EmptySessionPersistence : Supabase.Gotrue.Interfaces.IGotrueSessionPersistence<Supabase.Gotrue.Session>
{
    public void SaveSession(Supabase.Gotrue.Session session) { }
    public void DestroySession() { }
    public Supabase.Gotrue.Session? LoadSession() => null;
}

/// <summary>A real keeper that only counts the refreshes asked for instead of calling GoTrue.</summary>
public sealed class RecordingSessionKeeper(Supabase.Client client, Supabase.Gotrue.Interfaces.IGotrueSessionPersistence<Supabase.Gotrue.Session> persistence, TimeProvider time)
    : SessionKeeper(client, persistence, time)
{
    public int RefreshNowCalls { get; private set; }

    public override Task RefreshNowAsync()
    {
        RefreshNowCalls++;
        return Task.CompletedTask;
    }
}
