using Blazored.LocalStorage;
using MauiWASM.Provider;
using Moq;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Xunit;

namespace MauiWASM.Tests.UnitTests.Provider;

/// <summary>
/// The provider is the web app's only session store, and the Supabase client loads from it while being built at
/// startup, so a bad stored value must sign the user out instead of stopping the app from booting.
/// </summary>
public class CustomSupabaseSessionProviderTests
{
    private const string SessionKey = "SUPABASE_SESSION";

    private readonly Dictionary<string, string> _storage = [];
    private readonly Mock<ISyncLocalStorageService> _localStorage = new();
    private readonly CustomSupabaseSessionProvider _provider;

    public CustomSupabaseSessionProviderTests()
    {
        _localStorage.Setup(s => s.ContainKey(It.IsAny<string>())).Returns((string key) => _storage.ContainsKey(key));
        _localStorage.Setup(s => s.GetItem<string>(It.IsAny<string>())).Returns((string key) => _storage[key]);
        _localStorage.Setup(s => s.SetItem(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string key, string value) => _storage[key] = value);
        _localStorage.Setup(s => s.RemoveItem(It.IsAny<string>())).Callback((string key) => _storage.Remove(key));

        _provider = new CustomSupabaseSessionProvider(_localStorage.Object);
    }

    [Fact]
    public void SaveThenLoad_ReturnsTheSameSession()
    {
        var session = new Session
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 3600,
            CreatedAt = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc),
            User = new User { Id = "8c1d2a5e-0000-4000-8000-000000000001" }
        };

        _provider.SaveSession(session);
        var loaded = _provider.LoadSession();

        Assert.NotNull(loaded);
        Assert.Equal(session.AccessToken, loaded.AccessToken);
        Assert.Equal(session.RefreshToken, loaded.RefreshToken);
        Assert.Equal(session.ExpiresIn, loaded.ExpiresIn);
        Assert.Equal(session.ExpiresAt(), loaded.ExpiresAt());
        Assert.Equal(session.User.Id, loaded.User?.Id);
    }

    [Fact]
    public void NothingStored_ReturnsAnEmptySession()
    {
        var loaded = _provider.LoadSession();

        AssertEmpty(loaded);
    }

    [Fact]
    public void DestroySession_RemovesTheStoredSession()
    {
        _provider.SaveSession(new Session { AccessToken = "access-token" });

        _provider.DestroySession();

        Assert.False(_storage.ContainsKey(SessionKey));
        AssertEmpty(_provider.LoadSession());
    }

    [Fact]
    public void DestroySession_NothingStored_RemovesNothing()
    {
        _provider.DestroySession();

        _localStorage.Verify(s => s.RemoveItem(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"access_token\": {\"nested\": true}}")]
    public void CorruptJson_ReturnsAnEmptySessionAndDropsIt(string storedJson)
    {
        _storage[SessionKey] = storedJson;

        var loaded = _provider.LoadSession();

        AssertEmpty(loaded);
        Assert.False(_storage.ContainsKey(SessionKey));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    public void NullOrEmptyJson_ReturnsAnEmptySession(string storedJson)
    {
        _storage[SessionKey] = storedJson;

        AssertEmpty(_provider.LoadSession());
    }

    [Fact]
    public void JsonWithUnknownFields_StillLoadsTheKnownOnes()
    {
        _storage[SessionKey] = JsonConvert.SerializeObject(new
        {
            access_token = "access-token",
            refresh_token = "refresh-token",
            some_field_from_another_version = 42
        });

        var loaded = _provider.LoadSession();

        Assert.NotNull(loaded);
        Assert.Equal("access-token", loaded.AccessToken);
        Assert.Equal("refresh-token", loaded.RefreshToken);
    }

    private static void AssertEmpty(Session? session)
    {
        Assert.NotNull(session);
        Assert.Null(session.AccessToken);
        Assert.Null(session.RefreshToken);
        Assert.Null(session.User);
    }
}
