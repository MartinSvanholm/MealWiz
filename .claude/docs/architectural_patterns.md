# Architectural Patterns

## 1. Vertical-Slice Feature Organization

Each feature is a self-contained folder under `MealWiz.Shared/Features/`:

```
Features/
  Meals/
    Models/          # Domain model (Meal.cs) + DB model (MealDb.cs)
    SaveMeal/        # One folder per CQRS operation
    GetAllMeals/
    DeleteMeal/
    State/           # MealsStateContainer.cs (optional, for shared state)
    Components/      # Reusable sub-components
    MealsPage.razor  # Page-level component (@page directive)
    MealCard.razor   # Display-only component
```

All cross-feature dependencies go through MediatR commands/queries — features don't import each other's handlers directly.

---

## 2. CQRS with MediatR

Every data operation is a static class containing a nested record + handler:

```
Features/Meals/SaveMeal/SaveMeal.cs:9
Features/Meals/GetAllMeals/GetAllMeals.cs
Features/GroceryList/AddMealPlanIngredientsToGroceryList/AddMealPlanIngredientsToGroceryList.cs:9
```

**Structure:**
```csharp
public static class SaveMeal
{
    public record Command(Meal Meal) : IRequest<Result<Meal>>;   // line 11

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<Meal>>  // line 13
    {
        public async Task<Result<Meal>> Handle(Command request, CancellationToken ct) { ... }
    }
}
```

- Commands mutate data; Queries read data — same structural pattern for both.
- Handlers receive the Supabase `Client` via primary constructor injection.
- Dispatched from components/state containers: `await _mediator.Send(new SaveMeal.Command(meal))`.
- MediatR is registered to scan `MealWiz.Shared._Imports` assembly: `MealWiz.Web/Program.cs:20`.

---

## 3. Domain / DB Model Split

Every entity has two classes:

| Class | Purpose | Key detail |
|-------|---------|-----------|
| `Meal` | Domain model used by UI and handlers | Plain C# class |
| `MealDb` | Supabase Postgrest model | Extends `BaseModel`, decorated with `[Table]`, `[Column]`, `[PrimaryKey]` |

References:
- `Features/Meals/Models/Meal.cs:6` — domain model with two constructors + `MapToMealDb()`
- `Features/Meals/Models/MealDb.cs:7` — `[Table("meals")]` decorated Postgrest model

**Mapping convention:**
- `new Meal(mealDb)` — DB → domain (constructor, `Meal.cs:26`)
- `meal.MapToMealDb()` — domain → DB (instance method, `Meal.cs:52`)
- Nested references (e.g. ingredients) are mapped recursively via `ConvertAll`.

---

## 4. Result Pattern (FluentResults)

All Supabase calls are wrapped in `Result.Try(...)` — no raw exceptions propagate to components.

```csharp
// SaveMeal.cs:24
var insertResult = await Result.Try(async Task<ModeledResponse<MealDb>> () =>
    await supabaseClient.From<MealDb>().Insert(mealDb));

return insertResult.Map(r => new Meal(r.Model ?? new()));
```

**Error display:** `ResultHelper.Handle<T>` extension (`Helpers/ResultHelper.cs:39`) maps success/failure to MudBlazor `ISnackbar` notifications. State containers call it after every mediator send:

```csharp
// MealsStateContainer.cs:49
result.Handle(CurrentSnackbar);
```

**Global catch handler** configured at startup: `ResultHelper.SetDefaultCatchHandler()` (`ResultHelper.cs:10`) translates `GotrueException` and `PostgrestException` into `Error` objects, hiding stack traces in production.

---

## 5. State Containers

Shared mutable state lives in scoped DI services, not in components. Pattern used in Meals, MealPlans, and GroceryList features.

**Structure** (`Features/Meals/State/MealsStateContainer.cs`):
- Interface defined in the same file as implementation (lines 9–21).
- `event Action OnStateChanged` + `NotifyStateChanged()` for subscriber notification (line 27).
- Properties that should trigger re-renders call `NotifyStateChanged()` in their setter (line 36–39).
- `ISnackbar CurrentSnackbar` is set by the hosting page before loading data (allows snackbar from components that don't have direct page access).
- Async load methods send MediatR queries and update state in one place.

**Component subscription pattern:**
```razor
@implements IDisposable

protected override void OnInitialized()
{
    _state.OnStateChanged += StateHasChanged;
}

public void Dispose() => _state.OnStateChanged -= StateHasChanged;
```

---

## 6. Drawer State System

Side drawers (slide-in panels) are managed globally via `IDrawerStateContainer` (`Services/DrawerStateContainer/IDrawerStateContainer.cs`).

Key API:
- `OpenDrawer(string title, Type content)` — open a drawer with a component type as content
- `SwitchDrawerContent(Type content, ...)` — navigate within the same drawer
- `NavigateBack()` — pop the navigation stack
- `OpenConfirmDrawer(string title, string description)` — standardized confirm dialog
- `event ConfirmClickHandler? OnConfirmClick` — callback for confirm action

Drawers accept dynamic parameters via `Dictionary<string, object>` passed to `DrawerStateParameters`. The content `Type` is rendered dynamically by the layout using `DynamicComponent`.

---

## 7. DI Registration

Services are registered identically in both `MealWiz.Web/Program.cs` and `MealWiz.Web.Client/Program.cs` (WASM mirrors server). The shared registrations are:

```csharp
builder.Services.AddMudServices();
builder.Services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
builder.Services.AddScoped<IMealsStateContainer, MealsStateContainer>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWiz.Shared._Imports).Assembly));
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
```

`DependencyInjectionHelper.RegisterScopedServices()` (`Helpers/DependencyInjectionHelper.cs:9`) wraps the state container registrations for reuse across host projects.

New state containers must be added in **both** `Program.cs` files and in `DependencyInjectionHelper`.

---

## 8. Authentication

- `CustomAuthStateProvider` (`Services/Authentication/CustomAuthStateProvider.cs`) extends `AuthenticationStateProvider`.
- Reads the Supabase JWT from the session, extracts claims, and returns an `AuthenticationState`.
- `CustomSupabaseSessionProvider` (`MealWiz.Web/Providers/`) persists the session to `ISyncLocalStorageService` for SSR.
- Pages protected with `@attribute [Authorize]`; unauthenticated users are redirected by `Layout/RedirectToLogin.razor`.
- Database authorization is enforced by Supabase Row Level Security — see `supabase/migrations/*_rls.sql`.
