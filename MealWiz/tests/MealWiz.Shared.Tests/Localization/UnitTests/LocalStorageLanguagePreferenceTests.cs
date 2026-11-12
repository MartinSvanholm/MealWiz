using Blazored.LocalStorage;
using MealWiz.Shared.Localization;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

public class LocalStorageLanguagePreferenceTests
{
    private readonly Mock<ILocalStorageService> _storage = new();
    private readonly FakeNavigationManager _navigation = new();

    private LocalStorageLanguagePreference CreatePreference() => new(_storage.Object, _navigation);

    [Fact]
    public async Task GetAsync_StoredSupportedCode_ReturnsIt()
    {
        _storage.Setup(s => s.GetItemAsStringAsync(SupportedLanguages.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("da");

        Assert.Equal("da", await CreatePreference().GetAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("\"da\"")]
    public async Task GetAsync_NothingOrUnsupportedStored_ReturnsNullForDeviceDefault(string? stored)
    {
        _storage.Setup(s => s.GetItemAsStringAsync(SupportedLanguages.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        Assert.Null(await CreatePreference().GetAsync());
    }

    [Fact]
    public async Task SetAsync_SupportedCode_StoresItAsAPlainStringAndReloadsTheCurrentPage()
    {
        _navigation.NavigateTo("/account");

        await CreatePreference().SetAsync("da");

        _storage.Verify(s => s.SetItemAsStringAsync(SupportedLanguages.StorageKey, "da", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(("http://localhost/account", true), _navigation.LastNavigation);
    }

    [Fact]
    public async Task SetAsync_Null_RemovesTheKeyAndReloads()
    {
        await CreatePreference().SetAsync(null);

        _storage.Verify(s => s.RemoveItemAsync(SupportedLanguages.StorageKey, It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.SetItemAsStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(_navigation.LastNavigation?.ForceLoad);
    }

    [Fact]
    public async Task SetAsync_UnsupportedCode_ThrowsAndChangesNothing()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreatePreference().SetAsync("fr"));

        _storage.VerifyNoOtherCalls();
        Assert.Null(_navigation.LastNavigation);
    }

    private sealed class FakeNavigationManager : NavigationManager
    {
        public (string Uri, bool ForceLoad)? LastNavigation { get; private set; }

        public FakeNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).ToString();
            if (options.ForceLoad) LastNavigation = (Uri, true);
        }
    }
}
