using Blazored.LocalStorage;
using TheMealWorkshop.Shared.Theme;
using Moq;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.Theme.UnitTests;

public class ThemeServiceTests
{
    private readonly Mock<ILocalStorageService> _storage = new();

    [Fact]
    public async Task InitializeAsync_reads_the_stored_mode_and_notifies()
    {
        _storage.Setup(s => s.ContainKeyAsync("themealworkshop.theme", default)).ReturnsAsync(true);
        _storage.Setup(s => s.GetItemAsync<string>("themealworkshop.theme", default)).ReturnsAsync("dark");
        var service = new ThemeService(_storage.Object);
        var notified = 0;
        service.OnChange += () => notified++;

        await service.InitializeAsync();

        Assert.True(service.IsDarkMode);
        Assert.Equal(1, notified);
    }

    [Fact]
    public async Task InitializeAsync_moves_a_mode_saved_under_the_MealWiz_key()
    {
        _storage.Setup(s => s.ContainKeyAsync("mealwiz.theme", default)).ReturnsAsync(true);
        _storage.Setup(s => s.GetItemAsync<string>("mealwiz.theme", default)).ReturnsAsync("dark");
        _storage.Setup(s => s.SetItemAsync("themealworkshop.theme", "dark", default))
            .Callback(() =>
            {
                _storage.Setup(s => s.ContainKeyAsync("themealworkshop.theme", default)).ReturnsAsync(true);
                _storage.Setup(s => s.GetItemAsync<string>("themealworkshop.theme", default)).ReturnsAsync("dark");
            });
        var service = new ThemeService(_storage.Object);

        await service.InitializeAsync();

        Assert.True(service.IsDarkMode);
        _storage.Verify(s => s.RemoveItemAsync("mealwiz.theme", default), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_keeps_the_new_key_when_both_are_stored()
    {
        _storage.Setup(s => s.ContainKeyAsync(It.IsAny<string>(), default)).ReturnsAsync(true);
        _storage.Setup(s => s.GetItemAsync<string>("themealworkshop.theme", default)).ReturnsAsync("light");
        var service = new ThemeService(_storage.Object);

        await service.InitializeAsync();

        Assert.False(service.IsDarkMode);
        _storage.Verify(s => s.SetItemAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_defaults_to_light_when_nothing_is_stored()
    {
        var service = new ThemeService(_storage.Object);

        await service.InitializeAsync();

        Assert.False(service.IsDarkMode);
    }

    [Fact]
    public async Task ToggleAsync_flips_the_mode_stores_it_and_notifies()
    {
        var service = new ThemeService(_storage.Object);
        var notified = 0;
        service.OnChange += () => notified++;

        await service.ToggleAsync();

        Assert.True(service.IsDarkMode);
        _storage.Verify(s => s.SetItemAsync("themealworkshop.theme", "dark", default), Times.Once);
        Assert.Equal(1, notified);
    }
}
