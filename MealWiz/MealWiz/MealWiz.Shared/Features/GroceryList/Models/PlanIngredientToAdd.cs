using MealWiz.Shared.Features.Ingredients.Models;

namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>One planned ingredient to put on the list. <paramref name="MealName"/> is carried for display, not stored.</summary>
public record PlanIngredientToAdd(int PlannedMealId, Ingredient Ingredient, string MealName, int? ExistingGroceryItemId, string FinalAmount);
