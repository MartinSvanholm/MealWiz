using Blazored.LocalStorage;
using MealWiz.Shared.Theme;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Theme.UnitTests;

public class ThemeServiceTests
{
    private readonly Mock<ILocalStorageService> _storage = new();

    [Fact]
    public async Task InitializeAsync_reads_the_stored_mode_and_notifies()
    {
        _storage.Setup(s => s.ContainKeyAsync("mealwiz.theme", default)).ReturnsAsync(true);
        _storage.Setup(s => s.GetItemAsync<string>("mealwiz.theme", default)).ReturnsAsync("dark");
        var service = new ThemeService(_storage.Object);
        var notified = 0;
        service.OnChange += () => notified++;

        await service.InitializeAsync();

        Assert.True(service.IsDarkMode);
        Assert.Equal(1, notified);
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
        _storage.Verify(s => s.SetItemAsync("mealwiz.theme", "dark", default), Times.Once);
        Assert.Equal(1, notified);
    }
}
