using Blazored.LocalStorage;
using AddMealToMealPlanAction = MealWiz.Shared.Features.MealPlans.AddMealToMealPlan.AddMealToMealPlan;
using CreateMealPlanAction = MealWiz.Shared.Features.MealPlans.CreateMealPlan.CreateMealPlan;
using SwapMealInMealPlanAction = MealWiz.Shared.Features.MealPlans.SwapMealInMealPlan.SwapMealInMealPlan;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.MealPlans.State;

public interface IMealPlanStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    MealPlan? MealPlan { get; set; }
    DateTime SelectedDate { get; set; }
    DateTime SelectedWeekStart { get; }
    bool SelectedDateHasMeal { get; }
    bool IsLoadingWeek { get; }
    /// <summary>True while the user picks a meal to replace the selected day's meal. Ends when the day changes.</summary>
    bool IsSwappingMeal { get; }
    bool IsCurrentWeek();

    event Action OnStateChanged;

    void NotifyStateChanged();
    Task LoadMealPlan();
    Task NavigateToWeek(DateTime targetDate);
    /// <summary>Called by Plan week's Save once the saved week is loaded: the Meal plan then asks to add its ingredients (MEA-83).</summary>
    void OfferWeekIngredients(DateTime weekStart);
    /// <summary>The offered week's Monday, once, when that week is loaded and shown and has something for the list; else null.</summary>
    DateTime? TakeWeekIngredientsOffer();
    Task AddMealToPlan(Meal meal);
    void StartSwap();
    void CancelSwap();
    Task SwapMealOnSelectedDate(Meal meal);
    Meal? GetMealFromSelectedDate();
    bool DateHasMeal(DateTime dateTime);
}

public class MealPlanStateContainer(
    IMediator mediator,
    ILocalStorageService localStorage) : StateContainerBase, IMealPlanStateContainer
{
    private const string WeekStorageKey = "mealplan_selected_week";

    public MealPlan? MealPlan
    {
        get => mealPlan;
        set
        {
            mealPlan = value;
            NotifyStateChanged();
        }
    }
    private MealPlan? mealPlan { get; set; }

    public DateTime SelectedDate
    {
        get => selectedDate;
        set
        {
            selectedDate = value;
            IsSwappingMeal = false;
            NotifyStateChanged();
        }
    }
    private DateTime selectedDate { get; set; } = DateTime.Now;

    public DateTime SelectedWeekStart { get; private set; } = DateTime.Now.StartOfWeek().Date;

    public bool IsLoadingWeek { get; private set; } = true;

    public bool IsSwappingMeal { get; private set; }

    public bool IsCurrentWeek() =>
        SelectedWeekStart == DateTime.Now.StartOfWeek().Date;

    public async Task LoadMealPlan()
    {
        var result = await mediator.Send(new GetMealPlanByDate.GetMealPlanByDate.Query(SelectedWeekStart));
        result.Handle(CurrentSnackbar);

        // A failed load clears the plan rather than leaving the previous week's showing.
        MealPlan = result.ValueOrDefault;
    }

    public async Task NavigateToWeek(DateTime targetDate)
    {
        SelectedWeekStart = targetDate.StartOfWeek().Date;
        SelectedDate = IsCurrentWeek() ? DateTime.Now.Date : SelectedWeekStart;
        IsLoadingWeek = true;
        NotifyStateChanged();

        await localStorage.SetItemAsync(WeekStorageKey, SelectedWeekStart);
        await LoadMealPlan();

        IsLoadingWeek = false;
        NotifyStateChanged();
    }

    private DateTime? weekIngredientsOffer;

    public void OfferWeekIngredients(DateTime weekStart)
    {
        weekIngredientsOffer = weekStart.StartOfWeek().Date;
        NotifyStateChanged();
    }

    // Runs from OnAfterRender, so it must never notify.
    public DateTime? TakeWeekIngredientsOffer()
    {
        if (weekIngredientsOffer is not { } week) return null;
        if (IsLoadingWeek) return null; // keep it: a later render takes it

        weekIngredientsOffer = null;
        if (SelectedWeekStart != week || MealPlan is null) return null;

        var end = week.AddDays(7);
        var hasSomethingForTheList = MealPlan.MealOnDate.Any(kvp =>
            kvp.Key >= week && kvp.Key < end && kvp.Value.Ingredients.Any(i => i.ShowOnGroceryList));
        return hasSomethingForTheList ? week : null;
    }

    public async Task AddMealToPlan(Meal meal)
    {
        if (MealPlan is null)
        {
            var newPlan = new MealPlan(SelectedWeekStart);
            var createResult = await mediator.Send(new CreateMealPlanAction.Command(newPlan));
            if (createResult.Handle(CurrentSnackbar).IsFailed) return;

            MealPlan = createResult.Value;
        }

        meal.MealDate = SelectedDate.Date;
        meal.MealPlan = MealPlan;

        var result = await mediator.Send(new AddMealToMealPlanAction.Command(meal));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        MealPlan!.MealOnDate.TryAdd(meal.MealDate.Value, meal);
        MealPlan.PlannedMealIdOnDate[meal.MealDate.Value] = result.Value;
        NotifyStateChanged();
    }

    public void StartSwap()
    {
        IsSwappingMeal = true;
        NotifyStateChanged();
    }

    public void CancelSwap()
    {
        IsSwappingMeal = false;
        NotifyStateChanged();
    }

    public async Task SwapMealOnSelectedDate(Meal meal)
    {
        var current = GetMealFromSelectedDate();
        if (MealPlan is null || current is null) return;

        var date = SelectedDate.Date;
        var result = await mediator.Send(new SwapMealInMealPlanAction.Command(MealPlan.Id, date, current.Id, meal.Id));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        meal.MealDate = date;
        meal.MealPlan = MealPlan;
        MealPlan.MealOnDate[date] = meal;
        IsSwappingMeal = false;
        NotifyStateChanged();
    }

    public Meal? GetMealFromSelectedDate()
    {
        return GetMealFromDate(SelectedDate);
    }

    public bool DateHasMeal(DateTime dateTime)
    {
        return GetMealFromDate(dateTime.Date) != null;
    }

    private Meal? GetMealFromDate(DateTime dateTime)
    {
        Meal? meal = null;
        MealPlan?.MealOnDate.TryGetValue(dateTime.Date, out meal);

        return meal;
    }

    public bool SelectedDateHasMeal => GetMealFromSelectedDate() != null;
}
