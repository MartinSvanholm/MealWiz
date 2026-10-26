using MealWiz.Shared.Features.GroceryList.Models;
using Supabase;

namespace MealWiz.Shared.Tests.TestInfrastructure.Builders;

public sealed class GroceryItemBuilder
{
    private string _name = $"Test grocery item {ClientExtensions.UniqueSuffix()}";
    private string _amount = "1";
    private bool _isPicked;

    public GroceryItemBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public GroceryItemBuilder WithAmount(string amount)
    {
        _amount = amount;
        return this;
    }

    public GroceryItemBuilder Picked(bool isPicked = true)
    {
        _isPicked = isPicked;
        return this;
    }

    public GroceryItem Build() => new()
    {
        Name = _name,
        Amount = _amount,
        IsPicked = _isPicked
    };

    /// <summary>Inserts the item as the client's signed-in user.</summary>
    public async Task<GroceryItem> InsertAsync(Client client)
    {
        var groceryItemDb = Build().MapToDb();
        groceryItemDb.CreatedBy = client.CurrentUserId();
        groceryItemDb.CreatedAt = DateTime.UtcNow;

        var response = await client.From<GroceryItemDb>().Insert(groceryItemDb);
        return new GroceryItem(response.Model ?? throw new InvalidOperationException("Inserting a grocery item returned no row."));
    }
}
