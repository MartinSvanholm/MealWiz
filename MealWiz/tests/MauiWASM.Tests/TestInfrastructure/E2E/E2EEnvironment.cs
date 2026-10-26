using System.Diagnostics;
using System.Text;
using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
using Xunit;

[assembly: AssemblyFixture(typeof(E2EEnvironmentCleanup))]

namespace MauiWASM.Tests.TestInfrastructure.E2E;

/// <summary>
/// What every E2E test shares, set up once per test run on first use: the local Supabase stack, the published app
/// served by <see cref="E2EAppHost"/>, and one headless Chromium. Built lazily, so a run that filters the E2E tests
/// out never publishes the app or starts a browser.
/// <para>
/// If Supabase isn't running, <see cref="Unavailable"/> says why and the tests skip, unless REQUIRE_SUPABASE=true
/// (set in CI), in which case they fail. Anything that breaks after Supabase is found (publish, browser install)
/// always fails.
/// </para>
/// <para>
/// E2E_APP_DIR points at an existing publish output (the folder that contains wwwroot) to skip the publish step.
/// E2E_HEADED=true shows the browser.
/// </para>
/// </summary>
public sealed class E2EEnvironment : IAsyncDisposable
{
    private static readonly Lazy<Task<E2EEnvironment>> Shared = new(CreateAsync);

    private IPlaywright? _playwright;

    public string? Unavailable { get; private init; }
    public E2ESupabase Supabase { get; private init; } = null!;
    public E2EAppHost App { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public static Task<E2EEnvironment> GetAsync() => Shared.Value;

    internal static async Task DisposeIfCreatedAsync()
    {
        if (Shared.IsValueCreated && Shared.Value.IsCompletedSuccessfully)
            await Shared.Value.Result.DisposeAsync();
    }

    private static async Task<E2EEnvironment> CreateAsync()
    {
        var (supabase, unavailable) = await E2ESupabase.ConnectAsync();
        if (supabase is null)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("REQUIRE_SUPABASE"), "true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"REQUIRE_SUPABASE is set, but local Supabase isn't available: {unavailable}");

            return new E2EEnvironment { Unavailable = unavailable };
        }

        var environment = new E2EEnvironment { Supabase = supabase };

        var appDir = Environment.GetEnvironmentVariable("E2E_APP_DIR");
        if (string.IsNullOrWhiteSpace(appDir))
        {
            appDir = Path.Combine(AppContext.BaseDirectory, "e2e-app");
            await PublishAppAsync(appDir);
        }

        environment.App = await E2EAppHost.StartAsync(Path.Combine(appDir, "wwwroot"), supabase.Url, supabase.AnonKey);

        // A no-op once Chromium is installed. CI installs it beforehand with --with-deps for the Linux libraries.
        var installExitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (installExitCode != 0)
            throw new InvalidOperationException($"`playwright install chromium` exited with {installExitCode}.");

        environment._playwright = await Playwright.CreateAsync();
        environment.Browser = await environment._playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !string.Equals(Environment.GetEnvironmentVariable("E2E_HEADED"), "true", StringComparison.OrdinalIgnoreCase)
        });

        // A cold WASM boot on a CI runner takes a few seconds; Playwright's 5 s default is too tight.
        Assertions.SetDefaultExpectTimeout(15_000);

        return environment;
    }

    /// <summary>
    /// Publishes MauiWASM in Release, the build the deploy ships (trimmed, compressed), so the tests see what users
    /// get. Incremental, so it's quick when nothing changed.
    /// </summary>
    private static async Task PublishAppAsync(string outputDir)
    {
        var repoRoot = RepoPaths.FindRepoRoot()
            ?? throw new InvalidOperationException("Couldn't find the repo root (a directory containing supabase/config.toml).");
        var project = Path.Combine(repoRoot, "MealWiz", "MauiWASM", "MauiWASM.csproj");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "publish", project, "-c", "Release", "-o", outputDir, "--nologo" },
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // `dotnet test` can leave MSBuild variables in this process's environment that would point the child build
        // at the wrong SDK.
        foreach (var key in startInfo.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
            startInfo.Environment.Remove(key);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Couldn't start `dotnet publish`.");

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await process.WaitForExitAsync(timeout.Token);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"`dotnet publish {project}` failed with exit code {process.ExitCode}:\n{output}");
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
        if (App is not null) await App.DisposeAsync();
    }
}

/// <summary>Closes the shared browser and app host at the end of the run, if a test created them.</summary>
public sealed class E2EEnvironmentCleanup : IAsyncDisposable
{
    public async ValueTask DisposeAsync() => await E2EEnvironment.DisposeIfCreatedAsync();
}
