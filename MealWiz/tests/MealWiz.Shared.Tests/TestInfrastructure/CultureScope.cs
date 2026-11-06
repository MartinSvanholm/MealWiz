using System.Globalization;

namespace MealWiz.Shared.Tests.TestInfrastructure;

/// <summary>
/// Runs code under another culture: sets <see cref="CultureInfo.CurrentCulture"/> and
/// <see cref="CultureInfo.CurrentUICulture"/> for this async flow only and restores them on dispose. The run itself is
/// pinned to en-US (xunit.runner.json); use this for the few tests that need Danish.
/// <code>using var _ = CultureScope.Use("da");</code>
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture;
    private readonly CultureInfo _uiCulture;

    private CultureScope(CultureInfo culture)
    {
        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static CultureScope Use(string name) => new(CultureInfo.GetCultureInfo(name));

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}
