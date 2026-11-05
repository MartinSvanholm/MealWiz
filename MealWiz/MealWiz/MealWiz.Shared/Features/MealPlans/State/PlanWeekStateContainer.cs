using System.Text.Json;
using Blazored.LocalStorage;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.PlanWeek;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using GetAllMealsAction = MealWiz.Shared.Features.Meals.GetAllMeals.GetAllMeals;
using GetMealPlanByDateAction = MealWiz.Shared.Features.MealPlans.GetMealPlanByDate.GetMealPlanByDate;
using GetMealPlannedDatesAction = MealWiz.Shared.Features.MealPlans.GetMealPlannedDates.GetMealPlannedDates;
using SaveGeneratedWeekAction = MealWiz.Shared.Features.MealPlans.SaveGeneratedWeek.SaveGeneratedWeek;

namespace MealWiz.Shared.Features.MealPlans.State;

public interface IPlanWeekStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    event Action OnStateChanged;
    void NotifyStateChanged();

    DateTime ThisWeekStart { get; }
    DateTime WeekStart { get; }
    bool IsThisWeek { get; }
    PlanWeekSettings Settings { get; }
    IReadOnlyList<PlanWeekDay> Days { get; }
    IReadOnlyList<Meal> RegularMeals { get; }
    bool HasNoRegularMeals { get; }
    bool IsLoading { get; }
    bool IsGenerating { get; }
    bool IsSaving { get; }
    bool UsedFallback { get; }
    bool IsDirty { get; }
    bool CanGenerate { get; }
    bool CanSave { get; }

    /// <summary>Opens the page on the week containing <paramref name="requestedWeek"/> (never before this week).</summary>
    Task Open(DateTime requestedWeek);
    /// <summary>Moves to another week and drops the unsaved preview.</summary>
    Task NavigateToWeek(DateTime target);
    Task SetWeeksBack(int weeksBack);
    Task SetLeftoversAfterEveryMeal(bool leftoversAfterEveryMeal);
    void ToggleLock(DateTime date);
    Task Generate();
    /// <summary>Saves the changed days, then shows the week on the Meal plan. False when nothing was saved.</summary>
    Task<bool> Save();
}

public class PlanWeekStateContainer(
    IMediator mediator,
    ILocalStorageService localStorage,
    TimeProvider timeProvider,
    IMealPlanStateContainer mealPlanState,
    NavigationManager navigation) : StateContainerBase, IPlanWeekStateContainer
{
    internal const string SettingsStorageKey = "planweek_settings";

    internal Random Random { get; set; } = Random.Shared;

    private bool _settingsLoaded;
    private bool _mealsLoaded;
    private Meal? _leftovers;

    public DateTime ThisWeekStart => timeProvider.GetLocalNow().Date.StartOfWeek().Date;

    public DateTime WeekStart { get; private set; }

    public bool IsThisWeek => WeekStart == ThisWeekStart;

    public PlanWeekSettings Settings { get; private set; } = new();

    public IReadOnlyList<PlanWeekDay> Days { get; private set; } = [];

    public IReadOnlyList<Meal> RegularMeals { get; private set; } = [];

    public bool HasNoRegularMeals => _mealsLoaded && RegularMeals.Count == 0;

    public bool IsLoading { get; private set; }

    public bool IsGenerating { get; private set; }

    public bool IsSaving { get; private set; }

    public bool UsedFallback { get; private set; }

    public bool IsDirty => Days.Any(d => d.IsChanged);

    private bool IsBusy => IsLoading || IsGenerating || IsSaving;

    public bool CanGenerate => !IsBusy && Days.Count == 7 && RegularMeals.Count > 0;

    public bool CanSave => !IsBusy && Days.Any(d => d.IsChanged && d.Meal != null);

    public async Task Open(DateTime requestedWeek)
    {
        IsLoading = true;
        Days = [];
        UsedFallback = false;
        RegularMeals = [];
        _mealsLoaded = false;
        _leftovers = null;
        WeekStart = Clamp(requestedWeek);
        NotifyStateChanged();

        if (!_settingsLoaded)
        {
            _settingsLoaded = true;
            Settings = await ReadSettings();
        }

        var mealsResult = await mediator.Send(new GetAllMealsAction.Query());
        if (mealsResult.Handle(CurrentSnackbar).IsFailed)
        {
            IsLoading = false;
            NotifyStateChanged();
            return;
        }

        RegularMeals = mealsResult.Value.Where(m => m.IsRegular).ToList();
        _leftovers = mealsResult.Value.FirstOrDefault(m => m.Type == MealType.Leftover);
        _mealsLoaded = true;

        await LoadWeek();
    }

    public async Task NavigateToWeek(DateTime target)
    {
        WeekStart = Clamp(target);
        Days = [];
        UsedFallback = false;
        IsLoading = true;
        NotifyStateChanged();

        await LoadWeek();
    }

    public async Task SetWeeksBack(int weeksBack)
    {
        weeksBack = Math.Clamp(weeksBack, PlanWeekSettings.MinWeeksBack, PlanWeekSettings.MaxWeeksBack);
        if (weeksBack == Settings.WeeksBack) return;

        await UpdateSettings(Settings with { WeeksBack = weeksBack });
    }

    public async Task SetLeftoversAfterEveryMeal(bool leftoversAfterEveryMeal)
    {
        if (leftoversAfterEveryMeal == Settings.LeftoversAfterEveryMeal) return;

        await UpdateSettings(Settings with { LeftoversAfterEveryMeal = leftoversAfterEveryMeal });
    }

    public void ToggleLock(DateTime date)
    {
        var index = IndexOf(date);
        if (index < 0 || Days[index].Meal is null) return;

        ReplaceDay(index, Days[index] with { IsLocked = !Days[index].IsLocked });
        NotifyStateChanged();
    }

    public async Task Generate()
    {
        if (!CanGenerate) return;

        IsGenerating = true;
        NotifyStateChanged();

        var weeksBack = Settings.WeeksBack;
        var datesResult = await mediator.Send(new GetMealPlannedDatesAction.Query(
            WeekStart.AddDays(-7 * weeksBack), WeekStart.AddDays(7 * weeksBack + 6)));
        if (datesResult.Handle(CurrentSnackbar).IsFailed)
        {
            IsGenerating = false;
            NotifyStateChanged();
            return;
        }

        var locked = Days.Where(d => d.IsLocked && d.Meal != null).ToDictionary(d => d.Date, d => d.Meal!);
        var generated = WeekGenerator.Generate(
            new WeekGeneratorInput(WeekStart, RegularMeals, datesResult.Value, locked, weeksBack,
                Settings.LeftoversAfterEveryMeal, _leftovers),
            Random);

        Days = Days.Select((day, i) => day with { Meal = generated.Days[i].Meal }).ToList();
        UsedFallback = generated.UsedFallback;
        IsGenerating = false;
        NotifyStateChanged();
    }

    public async Task<bool> Save()
    {
        if (!CanSave) return false;

        IsSaving = true;
        NotifyStateChanged();

        var days = Days.Where(d => d.IsChanged && d.Meal != null)
            .Select(d => new SaveGeneratedWeekAction.PlannedDay(d.Date, d.Meal!.Id))
            .ToList();
        var result = await mediator.Send(new SaveGeneratedWeekAction.Command(WeekStart, days));
        IsSaving = false;

        if (result.Handle(CurrentSnackbar).IsFailed)
        {
            NotifyStateChanged();
            return false;
        }

        Days = Days.Select(d => d.Meal is null ? d : d with { SavedMealId = d.Meal.Id }).ToList();
        NotifyStateChanged();

        await mealPlanState.NavigateToWeek(WeekStart);
        navigation.NavigateTo("/");
        return true;
    }

    private DateTime Clamp(DateTime week)
    {
        var start = week.StartOfWeek().Date;
        return start < ThisWeekStart ? ThisWeekStart : start;
    }

    private async Task LoadWeek()
    {
        var week = WeekStart;
        var result = await mediator.Send(new GetMealPlanByDateAction.Query(week));

        // The user stepped to another week meanwhile; that week's load owns the state now.
        if (WeekStart != week) return;

        if (result.Handle(CurrentSnackbar).IsFailed)
        {
            Days = [];
        }
        else
        {
            var plan = result.ValueOrDefault;
            Days = Enumerable.Range(0, 7).Select(i =>
            {
                var date = week.AddDays(i);
                Meal? meal = null;
                plan?.MealOnDate.TryGetValue(date, out meal);
                return new PlanWeekDay(date, meal, meal?.Id, IsLocked: meal != null);
            }).ToList();
        }

        IsLoading = false;
        NotifyStateChanged();
    }

    private async Task<PlanWeekSettings> ReadSettings()
    {
        try
        {
            var stored = await localStorage.GetItemAsync<PlanWeekSettings>(SettingsStorageKey);
            return (stored ?? new PlanWeekSettings()).Clamped();
        }
        catch (JsonException)
        {
            return new PlanWeekSettings();
        }
    }

    private async Task UpdateSettings(PlanWeekSettings settings)
    {
        Settings = settings;
        await localStorage.SetItemAsync(SettingsStorageKey, settings);
        NotifyStateChanged();
    }

    private int IndexOf(DateTime date) => Days.ToList().FindIndex(d => d.Date == date.Date);

    private void ReplaceDay(int index, PlanWeekDay day)
    {
        var days = Days.ToList();
        days[index] = day;
        Days = days;
    }
}
