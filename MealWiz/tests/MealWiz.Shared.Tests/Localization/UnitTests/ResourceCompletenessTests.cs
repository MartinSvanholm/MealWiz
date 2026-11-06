using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using MealWiz.Shared.Localization;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

/// <summary>
/// Every resource the Shared assembly ships has a complete Danish translation: the same keys, no empty values and the
/// same <c>{n}</c> placeholders. Reads the embedded resources and the <c>da</c> satellite, so it also catches a .resx
/// that isn't embedded under the name its marker class expects (UI-03).
/// </summary>
public partial class ResourceCompletenessTests
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da");

    private static IEnumerable<string> EmbeddedBaseNames() =>
        typeof(SharedResource).Assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".resources", StringComparison.Ordinal))
            .Select(n => n[..^".resources".Length]);

    public static TheoryData<string> BaseNames() => new(EmbeddedBaseNames());

    [Fact]
    public void SharedResource_IsEmbeddedUnderItsTypeName()
    {
        Assert.Contains(typeof(SharedResource).FullName!, EmbeddedBaseNames());
    }

    [Theory]
    [MemberData(nameof(BaseNames))]
    public void Resource_HasACompleteDanishTranslation(string baseName)
    {
        var manager = new ResourceManager(baseName, typeof(SharedResource).Assembly);
        var english = Entries(manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true));
        var danishSet = manager.GetResourceSet(Danish, createIfNotExists: true, tryParents: false);
        Assert.True(danishSet is not null, $"{baseName} has no Danish (.da.resx) resource");
        var danish = Entries(danishSet);

        Assert.Empty(english.Keys.Except(danish.Keys).Select(k => $"{baseName}: {k} has no Danish value"));
        Assert.Empty(danish.Keys.Except(english.Keys).Select(k => $"{baseName}: {k} is Danish only"));
        Assert.Empty(danish.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => $"{baseName}: {e.Key} is empty in Danish"));
        Assert.Empty(english.Keys.Intersect(danish.Keys)
            .Where(k => !Placeholders(english[k]).SetEquals(Placeholders(danish[k])))
            .Select(k => $"{baseName}: {k} has different placeholders in English and Danish"));
    }

    [Theory]
    [MemberData(nameof(BaseNames))]
    public void PluralKeys_ComeInPairs(string baseName)
    {
        var manager = new ResourceManager(baseName, typeof(SharedResource).Assembly);
        var keys = Entries(manager.GetResourceSet(CultureInfo.InvariantCulture, true, true)).Keys.ToHashSet();

        Assert.Empty(keys.Where(k => k.EndsWith("_One", StringComparison.Ordinal) && !keys.Contains(k[..^"_One".Length] + "_Other"))
            .Select(k => $"{baseName}: {k} has no _Other form"));
        Assert.Empty(keys.Where(k => k.EndsWith("_Other", StringComparison.Ordinal) && !keys.Contains(k[..^"_Other".Length] + "_One"))
            .Select(k => $"{baseName}: {k} has no _One form"));
    }

    private static Dictionary<string, string> Entries(ResourceSet? set) =>
        set is null
            ? []
            : set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty);

    private static HashSet<string> Placeholders(string value) =>
        PlaceholderPattern().Matches(value).Select(m => m.Groups[1].Value).ToHashSet();

    [GeneratedRegex(@"\{(\d+)[^}]*\}")]
    private static partial Regex PlaceholderPattern();
}
