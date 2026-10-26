using FluentResults;
using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.Meals.State;
using MediatR;
using Moq;
using MudBlazor;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.UnitTests.State;

public class MealsStateContainerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly MealsStateContainer _container;
    private int _notifications;

    public MealsStateContainerTests()
    {
        _container = new MealsStateContainer(_mediator.Object) { CurrentSnackbar = _snackbar.Object };
        _container.OnStateChanged += () => _notifications++;
    }

    [Fact]
    public async Task LoadMeals_Success_SetsMealsAndNotifies()
    {
        List<Meal> meals = [new Meal { Id = 1, Name = "Pasta" }, new Meal { Id = 2, Name = "Soup" }];
        _mediator
            .Setup(m => m.Send(It.IsAny<GetAllMeals.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(meals));

        await _container.LoadMeals();

        Assert.Same(meals, _container.Meals);
        Assert.Equal(1, _notifications);
        VerifyNoToast();
    }

    [Fact]
    public async Task LoadMeals_Failure_ShowsAnErrorAndKeepsTheMeals()
    {
        List<Meal> existing = [new Meal { Id = 1 }];
        _container.Meals = existing;
        _notifications = 0;
        _mediator
            .Setup(m => m.Send(It.IsAny<GetAllMeals.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<List<Meal>>("Load failed"));

        await _container.LoadMeals();

        Assert.Same(existing, _container.Meals);
        Assert.Equal(0, _notifications);
        VerifyToast("Load failed", Severity.Error);
    }

    [Fact]
    public void AddOrUpdatePendingIngredient_AddsANewIngredientOnceAndNotifies()
    {
        var ingredient = new Ingredient { Name = "salt" };

        _container.AddOrUpdatePendingIngredient(ingredient);
        _container.AddOrUpdatePendingIngredient(ingredient);

        Assert.Same(ingredient, Assert.Single(_container.MealToEdit.Ingredients));
        Assert.Equal(2, _notifications);
    }

    [Fact]
    public void RemovePendingIngredient_RemovesItAndNotifies()
    {
        var salt = new Ingredient { Name = "salt" };
        var pepper = new Ingredient { Name = "pepper" };
        _container.MealToEdit.Ingredients.AddRange([salt, pepper]);

        _container.RemovePendingIngredient(salt);

        Assert.Same(pepper, Assert.Single(_container.MealToEdit.Ingredients));
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task ReloadIngredientsForMealToEdit_Success_ReplacesTheIngredients()
    {
        _container.MealToEdit = new Meal { Id = 7, Ingredients = [new Ingredient { Name = "stale" }] };
        List<Ingredient> fresh = [new Ingredient { Id = 1, Name = "salt", MealId = 7 }];
        _mediator
            .Setup(m => m.Send(It.IsAny<GetIngredientsByMealId.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(fresh));

        await _container.ReloadIngredientsForMealToEdit();

        Assert.Same(fresh, _container.MealToEdit.Ingredients);
        Assert.Equal(1, _notifications);
        _mediator.Verify(m => m.Send(new GetIngredientsByMealId.Query(7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReloadIngredientsForMealToEdit_Failure_ShowsAnErrorAndKeepsTheIngredients()
    {
        var existing = new Ingredient { Name = "salt" };
        _container.MealToEdit = new Meal { Id = 7, Ingredients = [existing] };
        _mediator
            .Setup(m => m.Send(It.IsAny<GetIngredientsByMealId.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<List<Ingredient>>("Load failed"));

        await _container.ReloadIngredientsForMealToEdit();

        Assert.Same(existing, Assert.Single(_container.MealToEdit.Ingredients));
        Assert.Equal(0, _notifications);
        VerifyToast("Load failed", Severity.Error);
    }

    [Fact]
    public void IsEdit_IsTrueOnlyForASavedMeal()
    {
        Assert.False(_container.IsEdit);

        _container.MealToEdit = new Meal { Id = 3 };

        Assert.True(_container.IsEdit);
    }

    [Fact]
    public void Meals_Setter_Notifies()
    {
        _container.Meals = [];

        Assert.Equal(1, _notifications);
    }

    private void VerifyToast(string message, Severity severity) =>
        _snackbar.Verify(s => s.Add(message, severity, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);

    private void VerifyNoToast() =>
        _snackbar.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Never);
}
