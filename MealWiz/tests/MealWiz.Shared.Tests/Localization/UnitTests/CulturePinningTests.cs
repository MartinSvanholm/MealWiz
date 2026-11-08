using System.Globalization;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

/// <summary>xunit.runner.json pins the run to en-US, so English text assertions hold on a Danish machine too.</summary>
public class CulturePinningTests
{
    [Fact]
    public void TestRun_IsPinnedToEnglish()
    {
        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
    }
}
