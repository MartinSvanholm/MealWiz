using System.Globalization;

namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>Which planned day and ingredient a grocery item was added or merged from, and the amount at the time.</summary>
public class GroceryItemSource
{
    public GroceryItemSource()
    {
        Amount = string.Empty;
    }

    public GroceryItemSource(GroceryItemSourceDetailsDb db)
    {
        Id = db.Id;
        GroceryItemId = db.GroceryItemId;
        PlannedMealId = db.PlannedMealId;
        IngredientId = db.IngredientId;
        Amount = db.Amount;
        CreatedBy = db.CreatedBy;
        CreatedAt = db.CreatedAt;
        PlannedDayMealId = db.PlannedDay?.MealId;
        PlannedDate = db.PlannedDay is null
            ? null
            : DateTime.Parse(db.PlannedDay.MealDate, CultureInfo.InvariantCulture);
        IngredientMealId = db.Ingredient?.MealId;
        IngredientName = db.Ingredient?.Name;
        IngredientAmount = db.Ingredient is null ? null : db.Ingredient.Amount ?? "";
        MealName = db.Ingredient?.Meal?.Name ?? db.PlannedDay?.Meal?.Name;
    }

    public int Id { get; set; }
    public int GroceryItemId { get; set; }
    /// <summary>The meal_plans-meals row; null once the meal was removed from the plan.</summary>
    public int? PlannedMealId { get; set; }
    /// <summary>The ingredient; null once it was deleted.</summary>
    public int? IngredientId { get; set; }
    public string Amount { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>The meal now planned on the source's day.</summary>
    public int? PlannedDayMealId { get; set; }
    public DateTime? PlannedDate { get; set; }
    /// <summary>The meal the source's ingredient belongs to.</summary>
    public int? IngredientMealId { get; set; }
    public string? IngredientName { get; set; }
    public string? MealName { get; set; }
    /// <summary>The ingredient's amount right now; null without a joined ingredient.</summary>
    public string? IngredientAmount { get; set; }

    /// <summary>The planned day or the ingredient is gone, or the day now holds another meal than the ingredient's.</summary>
    public bool IsStale => PlannedMealId is null || IngredientId is null || PlannedDayMealId != IngredientMealId;

    /// <summary>The ingredient's amount differs from the one remembered when it was added (trimmed, ignoring case).</summary>
    public bool HasChangedAmount =>
        !IsStale && IngredientAmount is not null
        && !string.Equals(Amount.Trim(), IngredientAmount.Trim(), StringComparison.OrdinalIgnoreCase);

    public GroceryItemSourceDb MapToGroceryItemSourceDb() => new()
    {
        Id = Id,
        GroceryItemId = GroceryItemId,
        PlannedMealId = PlannedMealId,
        IngredientId = IngredientId,
        Amount = Amount,
        CreatedBy = CreatedBy,
        CreatedAt = CreatedAt
    };
}
