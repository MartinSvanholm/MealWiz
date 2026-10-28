# When to mock

<!-- Adapted from mattpocock/skills `skills/engineering/tdd/mocking.md` (MIT). See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

Mock at **system boundaries** only. In MealWiz (Moq is the mocking library):

| Mock it | Don't mock it |
|---|---|
| `IMediator`, in state container and bUnit tests: it's the boundary between UI and handlers | Your own state containers, helpers or models |
| HTTP to the Function or to recipe sites (a fake `HttpMessageHandler`, or the Functions tests' local recipe server) | Supabase in integration tests: use local Supabase, so RLS is tested too |
| Time: `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) | Anything you control |
| Navigation and JS interop in bUnit, through bUnit's own fakes | |

Handler logic that needs no database (validation, mapping, parsing) is pulled into a pure method or model and unit
tested without mocks. A handler that needs a mocked `Client` to test is a sign the logic belongs in a model.

## Designing for mockability

**1. Inject dependencies.** Pass boundaries in through the constructor, registered in DI (ARCH-08), rather than
creating them inside.

```csharp
// Easy to test: the clock is injected
public class Handler(Client client, TimeProvider time) : IRequestHandler<Command, Result<Meal>> { … }

// Hard to test: the clock is global
var now = DateTime.UtcNow;
```

**2. Prefer specific requests over a generic one.** One MediatR request per operation (`SaveMeal.Command`,
`GetAllMeals.Query`) rather than one request with a mode switch. Each mock then returns one shape, setups need no
conditionals, and the test shows which operation it exercises.
