using Blazored.LocalStorage;

namespace TheMealWorkshop.Shared.Theme;

public class ThemeService(ILocalStorageService localStorage) : IThemeService
{
    internal const string StorageKey = "themealworkshop.theme";

    /// <summary>The key from when the app was called MealWiz; a mode stored under it moves to <see cref="StorageKey"/>.</summary>
    internal const string OldStorageKey = "mealwiz.theme";
    private bool _initialized;

    public bool IsDarkMode { get; private set; }

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await MoveOldKeyAsync();

        if (await localStorage.ContainKeyAsync(StorageKey))
        {
            var stored = await localStorage.GetItemAsync<string>(StorageKey);
            IsDarkMode = string.Equals(stored, "dark", StringComparison.OrdinalIgnoreCase);
        }

        _initialized = true;
        OnChange?.Invoke();
    }

    private async Task MoveOldKeyAsync()
    {
        if (await localStorage.ContainKeyAsync(StorageKey) || !await localStorage.ContainKeyAsync(OldStorageKey)) return;

        await localStorage.SetItemAsync(StorageKey, await localStorage.GetItemAsync<string>(OldStorageKey));
        await localStorage.RemoveItemAsync(OldStorageKey);
    }

    public async Task ToggleAsync()
    {
        IsDarkMode = !IsDarkMode;
        await localStorage.SetItemAsync(StorageKey, IsDarkMode ? "dark" : "light");
        OnChange?.Invoke();
    }
}
