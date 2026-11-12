using System.Reflection;
using System.Text.RegularExpressions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MauiWASM.Tests.UnitTests.Routing;

public partial class RouteTests
{
    private static readonly Assembly SharedAssembly = typeof(MealWiz.Shared._Imports).Assembly;
    private static readonly Assembly WebAssembly = typeof(App).Assembly;

    [Fact]
    public void EveryPageRoute_IsUnique()
    {
        var routes = new[] { SharedAssembly, WebAssembly }
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>()
                .Select(route => (Page: type.FullName, route.Template, Normalized: Normalize(route.Template))))
            .ToList();

        Assert.NotEmpty(routes);

        // The Router throws at runtime ("ambiguous routes") when two pages match the same URL.
        var duplicates = routes
            .GroupBy(r => r.Normalized)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(r => $"{r.Page} ({r.Template})"))}")
            .ToList();

        Assert.True(duplicates.Count == 0, "Ambiguous routes:\n" + string.Join("\n", duplicates));
    }

    [Fact]
    public void Router_FindsThePagesInTheSharedAssembly()
    {
        using var context = new BunitContext();
        context.AddAuthorization();
        context.Services.AddLocalization(); // App's route texts come from the shared resource
        // App wraps the router in the startup gate, which starts the session keeper (nothing stored: done at once).
        context.Services.AddSingleton(TimeProvider.System);
        context.Services.AddSingleton(new MealWiz.Shared.Services.Authentication.SessionKeeper(
            new Supabase.Client("http://localhost", "test-key"), new NoSession(), TimeProvider.System));
        context.ComponentFactories.AddStub<Router>();

        var router = context.Render<App>().FindComponent<Stub<Router>>().Instance.Parameters;

        Assert.Equal(WebAssembly, router.Get(r => r.AppAssembly));
        Assert.Contains(SharedAssembly, router.Get(r => r.AdditionalAssemblies)!);
    }

    private sealed class NoSession : Supabase.Gotrue.Interfaces.IGotrueSessionPersistence<Supabase.Gotrue.Session>
    {
        public void SaveSession(Supabase.Gotrue.Session session) { }
        public void DestroySession() { }
        public Supabase.Gotrue.Session? LoadSession() => null;
    }

    // "/meals/{id:guid}" and "/Meals/{mealId}/" match the same URLs, so they normalize to the same string.
    private static string Normalize(string template) =>
        RouteParameter().Replace(template.Trim('/').ToLowerInvariant(), "{}");

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex RouteParameter();
}
