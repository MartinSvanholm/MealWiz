namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>Grocery items listed under one heading (the meal they came from) in the review drawer.</summary>
public sealed record GroceryItemGroup(string Heading, IReadOnlyList<GroceryItem> Items);
