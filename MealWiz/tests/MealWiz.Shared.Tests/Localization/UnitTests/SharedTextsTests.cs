using MealWiz.Shared.Localization;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

public class SharedTextsTests
{
    [Fact]
    public void Get_ReadsTheCurrentUICulture()
    {
        Assert.Equal("Cancel", SharedTexts.Get("Common_Cancel"));

        using (CultureScope.Use("da"))
            Assert.Equal("Annuller", SharedTexts.Get("Common_Cancel"));
    }

    [Fact]
    public void Get_UnknownKey_ReturnsTheKey()
    {
        Assert.Equal("No_Such_Key", SharedTexts.Get("No_Such_Key"));
    }
}
