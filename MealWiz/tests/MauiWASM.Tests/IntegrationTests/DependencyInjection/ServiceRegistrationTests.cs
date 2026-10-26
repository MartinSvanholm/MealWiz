using System.Reflection;
using MauiWASM.Tests.TestInfrastructure;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Services.Authentication;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MauiWASM.Tests.IntegrationTests.DependencyInjection;

/// <summary>
/// Builds the web app's real container (AddMealWizWeb, which is all Program.cs registers) and resolves everything
/// the app asks for at runtime. A missing registration compiles fine but crashes the page that needs it.
/// </summary>
public class ServiceRegistrationTests
{
    private static readonly Assembly[] AppAssemblies = [typeof(MealWiz.Shared._Imports).Assembly, typeof(App).Assembly];

    [Fact]
    public Task EveryMediatRHandler_Resolves() =>
        AssertAllResolve(AppAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() is var definition
                && (definition == typeof(IRequestHandler<,>) || definition == typeof(IRequestHandler<>)
                    || definition == typeof(INotificationHandler<>))),
            expectAny: true);

    // Library components the app renders inject services too (AuthorizeRouteView needs AddAuthorizationCore's).
    private static readonly Assembly[] ComponentAssemblies =
    [
        .. AppAssemblies,
        typeof(AuthorizeRouteView).Assembly,
        typeof(MudBlazor.MudComponentBase).Assembly,
    ];

    [Fact]
    public Task EveryComponentInjection_Resolves() =>
        AssertAllResolve(ComponentAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(InjectedTypes),
            expectAny: true);

    [Fact]
    public async Task AppServices_Resolve()
    {
        await AssertAllResolve(
        [
            typeof(AuthenticationStateProvider),
            typeof(Supabase.Client),
            typeof(MealImportFunctionOptions),
            typeof(TimeProvider),
            typeof(HttpClient),
        ]);

        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<CustomAuthStateProvider>(scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>());
    }

    private static async Task AssertAllResolve(IEnumerable<Type> serviceTypes, bool expectAny = false)
    {
        var types = serviceTypes.Distinct().ToList();
        if (expectAny)
            Assert.NotEmpty(types);

        // Some MudBlazor services only implement IAsyncDisposable, so the container has to be disposed async.
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var failures = new List<string>();
        foreach (var type in types)
        {
            try
            {
                scope.ServiceProvider.GetRequiredService(type);
            }
            catch (Exception ex)
            {
                failures.Add($"{type}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} service(s) failed to resolve:\n" + string.Join("\n", failures));
    }

    private static ServiceProvider BuildProvider() => new ServiceCollection()
        .AddFakeWebHostServices()
        .AddMealWizWeb(WebHostServices.Configuration)
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    // @inject compiles to a private [Inject] property, so walk the hierarchy to see the base classes' ones too.
    private static IEnumerable<Type> InjectedTypes(Type component)
    {
        for (var type = component; type is not null && type != typeof(object); type = type.BaseType)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (property.IsDefined(typeof(InjectAttribute)))
                    yield return property.PropertyType;
            }
        }
    }
}
