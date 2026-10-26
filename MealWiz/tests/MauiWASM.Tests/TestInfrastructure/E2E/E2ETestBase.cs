using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace MauiWASM.Tests.TestInfrastructure.E2E;

/// <summary>
/// Base class for the browser tests. Each test gets its own throwaway user and its own browser context (so its own
/// localStorage and session), so tests are isolated and test classes run in parallel.
/// <para>
/// Waiting rules, so the tests don't flake: only Playwright's auto-waiting locators and <c>Expect</c> assertions.
/// No sleeps and no load-state waits; after a navigation, wait for <c>app-shell</c> (rendered once Blazor has
/// booted) and then for something on the page itself.
/// </para>
/// <para>
/// When a test fails, a Playwright trace, a screenshot and the browser console are saved to
/// <c>TestResults/e2e/&lt;Class.Method&gt;/</c> next to the test binaries. Open the trace with
/// <c>pwsh bin/Debug/net10.0/playwright.ps1 show-trace &lt;path&gt;/trace.zip</c>.
/// </para>
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly List<string> _console = [];
    private IBrowserContext? _context;

    protected E2EEnvironment Env { get; private set; } = null!;
    protected E2ESupabase Supabase => Env.Supabase;
    protected E2EUser User { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Env = await E2EEnvironment.GetAsync();
        Assert.SkipWhen(Env.Unavailable is not null, Env.Unavailable ?? string.Empty);

        User = await Supabase.CreateUserAsync();

        _context = await Env.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Env.App.BaseUrl.ToString(),
            // The app is built for phones: bottom navigation bar, bottom drawers.
            ViewportSize = new ViewportSize { Width = 412, Height = 915 }
        });
        _context.SetDefaultTimeout(15_000);

        await _context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = false });

        Page = await _context.NewPageAsync();
        Page.Console += (_, message) => Log($"[{message.Type}] {message.Text}");
        Page.PageError += (_, error) => Log($"[pageerror] {error}");
        Page.RequestFailed += (_, request) => Log($"[requestfailed] {request.Method} {request.Url}: {request.Failure}");
        // Supabase Realtime traffic, so a missed update can be told apart from one the app ignored.
        Page.WebSocket += (_, socket) =>
        {
            Log($"[websocket] open {socket.Url}");
            socket.FrameSent += (_, frame) => LogFrame("sent", frame.Text);
            socket.FrameReceived += (_, frame) => LogFrame("received", frame.Text);
            socket.Close += (_, _) => Log($"[websocket] closed {socket.Url}");
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            if (TestContext.Current.TestState?.Result == TestResult.Failed)
                await SaveFailureArtifactsAsync(_context);
            else
                await _context.Tracing.StopAsync();

            await _context.CloseAsync();
        }

        if (User is not null)
            await Supabase.DeleteUserAsync(User);
    }

    protected static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

    protected static IPageAssertions Expect(IPage page) => Assertions.Expect(page);

    /// <summary>Opens <paramref name="path"/> and waits until Blazor has booted and rendered the layout.</summary>
    protected async Task GoToAsync(string path)
    {
        await Page.GotoAsync(path);
        await Expect(Page.GetByTestId("app-shell")).ToBeVisibleAsync();
    }

    /// <summary>Signs <see cref="User"/> in through the login form and waits for the meal plan page.</summary>
    protected async Task LoginAsync()
    {
        await GoToAsync("login");

        await FillAsync(Page.GetByTestId("login-email"), User.Email);
        await FillAsync(Page.GetByTestId("login-password"), User.Password);
        await Page.GetByTestId("login-submit").ClickAsync();

        await Expect(Page.GetByTestId("meal-plan-week-title")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Types into a MudTextField and commits it. Without <c>Immediate</c> a MudTextField only updates its bound value
    /// on <c>change</c>, which fires on blur.
    /// </summary>
    protected static async Task FillAsync(ILocator input, string value)
    {
        await input.FillAsync(value);
        await input.BlurAsync();
    }

    /// <summary>Matches a path relative to the app's root, e.g. <c>"meals/edit"</c>, or <c>""</c> for the root.</summary>
    protected Regex UrlFor(string path) =>
        new($"^{Regex.Escape(new Uri(Env.App.BaseUrl, path).ToString())}$");

    /// <summary>
    /// The bottom drawer, only while it's open. A closed MudDrawer keeps its content in the DOM, just off-screen,
    /// so look inside this rather than at the whole page, or a test can type into the last, closed drawer.
    /// </summary>
    protected ILocator Drawer => Page.Locator("[data-testid=drawer][data-open]");

    /// <summary>The "are you sure" drawer, only while it's open.</summary>
    protected ILocator ConfirmDrawer => Page.Locator("[data-testid=confirm-drawer][data-open]");

    /// <summary>A list row (meal, grocery item, ingredient card) by its test id and the text it shows.</summary>
    protected ILocator Row(string testId, string text) => Page.GetByTestId(testId).Filter(new LocatorFilterOptions { HasText = text });

    private void LogFrame(string direction, string? text)
    {
        if (text is null || text.Contains("\"phoenix\"") && text.Contains("heartbeat")) return;
        Log($"[websocket] {direction} {(text.Length > 1000 ? text[..1000] + "…" : text)}");
    }

    private void Log(string line)
    {
        lock (_console) _console.Add($"{DateTime.UtcNow:HH:mm:ss.fff} {line}");
    }

    private async Task SaveFailureArtifactsAsync(IBrowserContext context)
    {
        var test = TestContext.Current.Test;
        var name = test is null ? Guid.NewGuid().ToString("N") : $"{GetType().Name}.{test.TestCase.TestMethod?.MethodName}";
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "e2e", string.Join("_", name.Split(Path.GetInvalidFileNameChars())));
        Directory.CreateDirectory(directory);

        await context.Tracing.StopAsync(new TracingStopOptions { Path = Path.Combine(directory, "trace.zip") });

        if (!Page.IsClosed)
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(directory, "screenshot.png"), FullPage = true });

        string[] lines;
        lock (_console) lines = [.. _console];
        await File.WriteAllLinesAsync(Path.Combine(directory, "console.log"), lines);

        TestContext.Current.SendDiagnosticMessage($"E2E failure artifacts: {directory}");
    }
}
