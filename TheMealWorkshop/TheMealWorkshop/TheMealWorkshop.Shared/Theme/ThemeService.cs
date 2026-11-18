using Blazored.LocalStorage;

namespace TheMealWorkshop.Shared.Theme;

public class ThemeService(ILocalStorageService localStorage) : IThemeService
{
    private const string StorageKey = "mealwiz.theme"; // the old app name, kept so saved choices survive the rename
    private bool _initialized;

    public bool IsDarkMode { get; private set; }

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        if (await localStorage.ContainKeyAsync(StorageKey))
        {
            var stored = await localStorage.GetItemAsync<string>(StorageKey);
            IsDarkMode = string.Equals(stored, "dark", StringComparison.OrdinalIgnoreCase);
        }

        _initialized = true;
        OnChange?.Invoke();
    }

    public async Task ToggleAsync()
    {
        IsDarkMode = !IsDarkMode;
        await localStorage.SetItemAsync(StorageKey, IsDarkMode ? "dark" : "light");
        OnChange?.Invoke();
    }
}
