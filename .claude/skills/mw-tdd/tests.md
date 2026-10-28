# Good and bad tests

<!-- Adapted from mattpocock/skills `skills/engineering/tdd/tests.md` (MIT). See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

## Good tests

**Integration-style**: drive the real interface (the handler through MediatR, as a signed-in user, through RLS).

```csharp
// GOOD: observable behaviour, through the handler
[Fact]
public async Task GetAllMeals_AfterSave_ReturnsTheNewMeal()
{
    Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
    var meal = new MealBuilder().WithName("Lasagne").Build();

    var saved = ResultAssert.Ok(await SaveAsync(meal));

    var meals = ResultAssert.Ok(await GetAllAsync());
    Assert.Contains(meals, m => m.Id == saved.Id && m.Name == "Lasagne");
}
```

Characteristics:

- Tests behaviour a user or caller cares about
- Uses the public interface only
- Survives internal refactors
- The name says WHAT (`Method_Scenario_Expected`), not HOW
- One logical assertion per test

## Bad tests

**Implementation-detail tests**: coupled to internal structure.

```csharp
// BAD: asserts on how the state container talks to MediatR
[Fact]
public async Task LoadMeals_CallsMediatorOnce()
{
    await _container.LoadMeals();
    _mediator.Verify(m => m.Send(It.IsAny<GetAllMeals.Query>(), It.IsAny<CancellationToken>()), Times.Once);
}

// GOOD: asserts on what the container exposes afterwards
[Fact]
public async Task LoadMeals_HandlerReturnsMeals_ExposesThem()
{
    _mediator.Setup(m => m.Send(It.IsAny<GetAllMeals.Query>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result.Ok(new List<Meal> { new Meal { Id = 1, Name = "Soup" } }));

    await _container.LoadMeals();

    Assert.Equal(["Soup"], _container.Meals.Select(m => m.Name));
}
```

Red flags:

- Mocking your own classes (state containers, handlers, helpers)
- Testing private methods
- Asserting on call counts or order
- Breaks on a refactor with no behaviour change
- The name describes HOW, not WHAT

**Reading the database instead of the interface.** Prefer checking a write through the handler that reads it back.
Reading the row directly (through the test user's client, so RLS still applies) is fine for what no handler exposes,
such as `created_by` or timestamps; `MealHandlerTests` does this.

**Tautological tests**: the expected value restates the implementation.

```csharp
// (An illustrative helper that sums amounts.)
// BAD: expected value computed the way the code computes it
var items = new[] { new GroceryItem { Amount = 2 }, new GroceryItem { Amount = 3 } };
Assert.Equal(items.Sum(i => i.Amount), GroceryList.TotalAmount(items));

// GOOD: an independent, known literal
Assert.Equal(5, GroceryList.TotalAmount(items));
```

**bUnit**: select by `data-testid` (UI-01), never by CSS class or markup position. Assert on what the user sees or
what was sent through the mocked `IMediator`, not on component fields.
