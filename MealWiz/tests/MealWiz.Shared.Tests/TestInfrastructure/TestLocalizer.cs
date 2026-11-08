using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Tests.TestInfrastructure;

/// <summary>
/// The real resource localizer, as the hosts register it (<c>AddLocalization()</c>), for tests that construct a
/// handler or state container themselves. Runs are pinned to en-US, so it returns the English texts; wrap a call in
/// <see cref="CultureScope.Use"/> for Danish.
/// </summary>
public static class TestLocalizer
{
    private static readonly IServiceProvider Services =
        new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();

    public static IStringLocalizer<T> For<T>() => Services.GetRequiredService<IStringLocalizer<T>>();
}
