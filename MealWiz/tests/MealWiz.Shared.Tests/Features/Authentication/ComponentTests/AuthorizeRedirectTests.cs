using System.Reflection;
using Bunit;
using MealWiz.Shared.Features.Account;
using MealWiz.Shared.Features.Authentication.Login;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.MealImport.ImportMeal;
using MealWiz.Shared.Features.MealPlans;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.Meals.EditMeal;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Authentication.ComponentTests;

/// <summary>[Authorize] pages: anonymous users are sent to /login, signed-in users get the page.</summary>
public class AuthorizeRedirectTests : ComponentTestBase
{
    // Pages that don't require sign-in. Adding one here should be a deliberate decision.
    private static readonly Type[] PublicPages = [typeof(LoginPage)];

    public static TheoryData<Type, string> ProtectedPages => new()
    {
        { typeof(MealPlanPage), "Week" },
        { typeof(MealsPage), "Your meals" },
        { typeof(EditMealPage), "Edit meal" },
        { typeof(ImportMealPage), "Import meal" },
        { typeof(GroceryListPage), "Grocery list" },
        { typeof(AccountPage), "Dark mode" },
    };

    [Theory]
    [MemberData(nameof(ProtectedPages))]
    public void Anonymous_users_are_redirected_to_login(Type pageType, string _)
    {
        Auth.SetNotAuthorized();

        Render(AuthTestHelper.RoutedPage(pageType));

        Assert.Equal("login", CurrentPath);
    }

    [Theory]
    [MemberData(nameof(ProtectedPages))]
    public void Signed_in_users_see_the_page(Type pageType, string expectedText)
    {
        Auth.SetAuthorized("someone@example.com");

        Render<MudBlazor.MudPopoverProvider>(); // MealsPage's add menu needs it
        var cut = Render(AuthTestHelper.RoutedPage(pageType));

        Assert.Contains(expectedText, cut.Markup);
        Assert.NotEqual("login", CurrentPath);
    }

    [Fact]
    public void Every_page_requires_sign_in_unless_listed_as_public()
    {
        var unprotected = typeof(MealsPage).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<RouteAttribute>() is not null)
            .Where(t => t.GetCustomAttribute<AuthorizeAttribute>() is null)
            .Except(PublicPages)
            .Select(t => t.Name);

        Assert.Empty(unprotected);
    }
}
