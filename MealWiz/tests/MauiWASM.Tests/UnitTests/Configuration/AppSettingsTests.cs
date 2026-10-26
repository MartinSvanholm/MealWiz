using Microsoft.Extensions.Configuration;
using Xunit;

namespace MauiWASM.Tests.UnitTests.Configuration;

/// <summary>
/// Reads the real wwwroot/appsettings.*.json files (linked into the output by the csproj) the way Program.cs does.
/// </summary>
public class AppSettingsTests
{
    public static TheoryData<string> Environments => ["Development", "Staging", "Production"];

    [Theory]
    [MemberData(nameof(Environments))]
    public void Supabase_HasAnAbsoluteUrlAndAPublishableKey(string environment)
    {
        var config = Load(environment);

        Assert.True(Uri.TryCreate(config["Supabase:url"], UriKind.Absolute, out _),
            $"Supabase:url in appsettings.{environment}.json is not an absolute URL.");
        // The key ships in the public bundle, so it must be the publishable one, never a secret or service-role key.
        Assert.StartsWith("sb_publishable_", config["Supabase:key"]);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public void MealImportFunction_HasUrlAndEmptyKeySlots(string environment)
    {
        var config = Load(environment);

        Assert.NotNull(config["MealImportFunction:url"]);
        // Production's key is injected by deploy.yml with jq; none is ever committed.
        Assert.Equal(string.Empty, config["MealImportFunction:key"]);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void MealImportFunction_UrlIsAbsolute(string environment)
    {
        var config = Load(environment);

        Assert.True(Uri.TryCreate(config["MealImportFunction:url"], UriKind.Absolute, out _),
            $"MealImportFunction:url in appsettings.{environment}.json is not an absolute URL.");
    }

    [Fact]
    public void Development_PointsAtLocalSupabase()
    {
        Assert.Equal("http://127.0.0.1:54321", Load("Development")["Supabase:url"]);
    }

    private static IConfiguration Load(string environment) => new ConfigurationBuilder()
        .SetBasePath(Path.Combine(AppContext.BaseDirectory, "appsettings"))
        .AddJsonFile($"appsettings.{environment}.json", optional: false)
        .Build();
}
