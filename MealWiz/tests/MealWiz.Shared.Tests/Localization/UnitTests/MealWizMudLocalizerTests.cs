using MealWiz.Shared.Localization;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

/// <summary>MudBlazor's built-in screen-reader texts the app shows ("Clear", snackbar "Close") come from the shared resource.</summary>
public class MealWizMudLocalizerTests
{
    private readonly MealWizMudLocalizer _localizer = new(
        new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResource>>());

    [Theory]
    [InlineData("MudInput_Clear", "Ryd")]
    [InlineData("MudSnackbar_Close", "Luk")]
    public void KnownKey_IsTranslated(string key, string danish)
    {
        using var _ = CultureScope.Use("da");

        var text = _localizer[key];

        Assert.False(text.ResourceNotFound);
        Assert.Equal(danish, text.Value);
    }

    [Fact]
    public void OtherKey_IsNotFound_SoMudBlazorKeepsItsEnglish()
    {
        using var _ = CultureScope.Use("da");

        Assert.True(_localizer["MudDataGrid_Contains"].ResourceNotFound);
        Assert.True(_localizer["MudDataGrid_Contains", 1].ResourceNotFound);
    }
}
