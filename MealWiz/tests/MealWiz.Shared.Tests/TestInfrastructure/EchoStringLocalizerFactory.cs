using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Tests.TestInfrastructure;

/// <summary>
/// Localizers that return <c>[Key]</c> for every key, so a bUnit test can assert which resource key a component shows
/// (a hard-coded text would show up as itself). Opt in with <c>ComponentTestBase(echoResourceKeys: true)</c>.
/// </summary>
public sealed class EchoStringLocalizerFactory : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource) => new EchoStringLocalizer();

    public IStringLocalizer Create(string baseName, string location) => new EchoStringLocalizer();

    private sealed class EchoStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, $"[{name}]");

        public LocalizedString this[string name, params object[] arguments] => new(name, $"[{name}]");

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
