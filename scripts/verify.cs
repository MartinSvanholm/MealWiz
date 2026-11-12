// One command that tells you (or an AI agent) whether the app still works. Run it through the shims:
//
//   scripts/verify.ps1 <fast|local|smoke|full>     (Windows)
//   scripts/verify.sh  <fast|local|smoke|full>     (Linux, macOS, Git Bash)
//
// They run this file as a .NET 10 file-based app (`dotnet run --file scripts/verify.cs -- <level>`), so both shells
// share one implementation. The levels:
//
//   fast   docs check + build + unit and bUnit tests. No Docker needed.
//   local  fast + local Supabase + integration tests + the pgTAP database tests (`supabase test db`). No browser
//          tests: CI runs those. `--e2e <class>` adds one E2E class, e.g. a journey that pins the bug being fixed.
//   smoke  fast + local Supabase + integration tests + the Smoke E2E tests.
//   full   smoke + every E2E journey + the pgTAP database tests.
//
// Every level that uses Supabase takes a machine-wide lock first, so parallel worktrees (agent sessions) take turns
// on the one local stack instead of slowing each other down and changing its schema under each other's tests. Under
// the lock it resets the database to this worktree's migrations and seed.sql (`supabase db reset`), unless the stack
// was just started, CI is set, or --no-reset is passed. It starts the stack without Studio (--studio adds it) and, on
// Linux as root (a cloud container), starts dockerd if Docker isn't running.
//
// The exit code is the verdict: 0 all passed, 1 something failed, 2 couldn't run (bad arguments, no Docker...).
// Only a short summary is printed. Full build and test logs, and a TRX per suite, go to artifacts/verify/<level>/.
//
// CI runs `full` in pieces, so each test project shows up as its own step in the PR check, and the suite list stays
// in this one file:
//
//   --only <steps>     run only these steps of the level (comma list: docs, build, shared-unit, ..., db; see --help).
//                      Without "build" the build is skipped, so build first.
//   --coverage         collect Cobertura coverage for the unit and integration suites (<suite>/<suite>.cobertura.xml)
//   --check-complete   fail if a step of the level hasn't run since the level's last full run, so CI.yml can't fall
//                      behind when a suite is added here
//
// On GitHub Actions the summary is also appended to the job summary.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

const int Passed = 0, Failed = 1, CouldNotRun = 2;

var level = args.FirstOrDefault()?.ToLowerInvariant();
if (level is null or "-h" or "--help" or "help")
{
    Console.WriteLine("""
        usage: scripts/verify.ps1 <level>   or   scripts/verify.sh <level>

          fast   docs check + build + unit and bUnit tests (no Docker)
          local  fast + local Supabase + integration tests + pgTAP database tests (no browser tests)
          smoke  fast + local Supabase + integration tests + Smoke E2E tests
          full   smoke + every E2E journey + pgTAP database tests

        Exit code 0 = passed, 1 = failed, 2 = couldn't run. Full logs: artifacts/verify/<level>/

          --e2e <class>      local only: also run this E2E class, e.g. --e2e GroceryListJourneyTests
          --no-reset         don't reset the local database to this worktree's migrations and seed first
          --studio           also start Supabase Studio (http://127.0.0.1:54323) when verify starts the stack

        For CI (see the top of scripts/verify.cs):
          --only <steps>     comma list of steps to run: docs, build, shared-unit, functions-unit, web-unit,
                             shared-integration, functions-integration, e2e-smoke (smoke), e2e (full, local --e2e),
                             db (local, full)
          --coverage         Cobertura coverage for the unit and integration suites
          --check-complete   fail if a step of the level hasn't run
        """);
    return level is null ? CouldNotRun : Passed;
}
if (level is not ("fast" or "local" or "smoke" or "full"))
{
    Console.WriteLine($"Unknown level '{args[0]}'. Use fast, local, smoke or full.");
    return CouldNotRun;
}

HashSet<string>? only = null;
var coverage = false;
var checkComplete = false;
var noReset = false;
var studio = false;
string? e2eClass = null;
for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--only" when i + 1 < args.Length:
            only = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            break;
        case "--coverage":
            coverage = true;
            break;
        case "--check-complete":
            checkComplete = true;
            break;
        case "--no-reset":
            noReset = true;
            break;
        case "--studio":
            studio = true;
            break;
        case "--e2e" when i + 1 < args.Length && level is "local":
            e2eClass = args[++i];
            break;
        case "--e2e":
            Console.WriteLine("--e2e <class> only goes with `local` (smoke and full already run their E2E tests).");
            return CouldNotRun;
        default:
            Console.WriteLine($"Unknown option '{args[i]}'. See scripts/verify.sh --help.");
            return CouldNotRun;
    }
}

var root = FindRepoRoot();
if (root is null)
{
    Console.WriteLine("Run this from inside the MealWiz repo (couldn't find supabase/config.toml above the current directory).");
    return CouldNotRun;
}

const string SharedTests = "MealWiz/tests/MealWiz.Shared.Tests";
const string FunctionsTests = "MealWiz/tests/MealWiz.Functions.Tests";
const string WebTests = "MealWiz/tests/MauiWASM.Tests";
// Where E2ETestBase saves a trace, screenshot and console log for each failed E2E test.
var e2eArtifacts = Path.Combine(root, WebTests, "bin", "Debug", "net10.0", "TestResults", "e2e");

// The test suites of this level. Coverage is off for E2E: the app runs as WebAssembly in Chromium, out of reach.
var suites = new List<(string Id, string Name, string Project, string[] Filter, bool Coverage)>
{
    ("shared-unit", "Shared unit + bUnit", SharedTests, ["--filter-not-trait", "Category=Integration"], true),
    ("functions-unit", "Functions unit", FunctionsTests, ["--filter-not-trait", "Category=Integration"], true),
    ("web-unit", "MauiWASM unit", WebTests, ["--filter-not-trait", "Category=E2E"], true),
};
if (level is not "fast")
{
    suites.Add(("shared-integration", "Shared integration", SharedTests, ["--filter-trait", "Category=Integration"], true));
    suites.Add(("functions-integration", "Functions integration", FunctionsTests, ["--filter-trait", "Category=Integration"], true));
    if (level is "smoke")
        suites.Add(("e2e-smoke", "E2E smoke", WebTests, ["--filter-trait", "Category=Smoke"], false));
    else if (level is "full")
        suites.Add(("e2e", "E2E (all journeys)", WebTests, ["--filter-trait", "Category=E2E"], false));
    else if (e2eClass is not null)
        // Different filter kinds are ANDed, so this is the E2E tests of that class.
        suites.Add(("e2e", $"E2E ({e2eClass})", WebTests, ["--filter-trait", "Category=E2E", "--filter-class", "*." + e2eClass], false));
}

// Every step of the level, in order, as --only names them.
var steps = new List<string> { "docs", "build" };
steps.AddRange(suites.Select(s => s.Id));
if (level is "local" or "full") steps.Add("db");

var outDir = Path.Combine(root, "artifacts", "verify", level);
// The steps run since the last full run of this level, one per line, for --check-complete.
var stepsRun = Path.Combine(outDir, ".steps-run");

if (checkComplete)
{
    var ran = File.Exists(stepsRun) ? File.ReadAllLines(stepsRun).ToHashSet() : [];
    var missing = steps.Where(s => !ran.Contains(s)).ToList();
    Console.WriteLine(missing.Count == 0
        ? $"verify {level}: every step ran ({string.Join(", ", steps)})"
        : $"verify {level}: these steps never ran: {string.Join(", ", missing)}. Add them to an --only list in .github/workflows/CI.yml.");
    return missing.Count == 0 ? Passed : Failed;
}

if (only is not null)
{
    var unknown = only.Where(s => !steps.Contains(s)).ToList();
    if (unknown.Count > 0)
    {
        Console.WriteLine($"Unknown step(s) for {level}: {string.Join(", ", unknown)}. The steps are: {string.Join(", ", steps)}.");
        return CouldNotRun;
    }
    suites.RemoveAll(s => !only.Contains(s.Id));
}

bool Selected(string step) => only is null || only.Contains(step);

// A full run starts clean. A partial one only replaces its own steps' output, so CI's steps add up.
if (only is null && Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
Directory.CreateDirectory(outDir);
foreach (var step in steps.Where(Selected))
{
    if (Directory.Exists(Path.Combine(outDir, step))) Directory.Delete(Path.Combine(outDir, step), recursive: true);
    File.Delete(Path.Combine(outDir, step + ".log"));
}
File.AppendAllLines(stepsRun, steps.Where(Selected));

var clock = Stopwatch.StartNew();
var summary = new List<string>();
var details = new List<string>();
var failed = false;
// The machine-wide lock on local Supabase (step 3), held until Finish. The OS releases it if this process dies.
FileStream? supabaseLock = null;
var lockPath = Path.Combine(Path.GetTempPath(), "mealwiz-verify-supabase.lock");
var lockTimeout = TimeSpan.FromMinutes(60);

// 1. Docs. The agent docs must name every feature folder, handler, table and verify step, and every path they put in
// backticks must exist (DOC-01 in docs/agents/ARCHITECTURE.md). A failure doesn't stop the run.
if (Selected("docs"))
{
    Progress("docs");
    var problems = CheckDocs();
    File.WriteAllLines(Path.Combine(outDir, "docs.log"), problems.Count == 0 ? ["ok"] : problems);
    if (problems.Count == 0)
        summary.Add(Row("docs", "ok"));
    else
    {
        failed = true;
        summary.Add(Row("docs", $"FAILED ({problems.Count} problem{(problems.Count == 1 ? "" : "s")})"));
        details.Add("Agent docs are out of date (DOC-01). Fix docs/agents/ or CLAUDE.md:");
        details.AddRange(problems.Take(15).Select(p => "  " + p));
    }
}

// 2. Build. The MAUI head in MealWiz.sln needs mobile workloads, so build what the tests reference instead
// (MealWiz.Shared, MauiWASM, MealWiz.Functions and the three test projects), like CI.
if (Selected("build"))
{
    Progress("build");
    var buildLog = Path.Combine(outDir, "build.log");
    foreach (var project in new[] { SharedTests, FunctionsTests, WebTests })
    {
        var exit = Run("dotnet", ["build", project, "-nologo", "-v:minimal", "-clp:NoSummary"], buildLog);
        if (exit != 0)
        {
            summary.Add(Row("build", $"FAILED ({project})"));
            details.Add("Build errors:");
            details.AddRange(BuildErrors(buildLog).Select(e => "  " + e));
            return Finish(Failed);
        }
    }
    summary.Add(Row("build", "ok"));
}

// 3. Supabase, for the steps that use it: integration, E2E and pgTAP. REQUIRE_SUPABASE makes the fixtures fail
// instead of skipping, so a broken stack can't pass as "all skipped".
// All worktrees share one local stack, so the lock is held from here until the run ends.
var env = new Dictionary<string, string>();
if (suites.Any(s => !s.Id.EndsWith("-unit")) || Selected("db") && level is "local" or "full")
{
    Progress("supabase");
    var waited = WaitForSupabaseLock();
    if (supabaseLock is null)
    {
        summary.Add(Row("supabase", "locked", waited));
        details.Add($"Another verify held local Supabase for over {lockTimeout.TotalMinutes:0} minutes ({LockOwner()}). " +
                    "Wait for it, or stop that process if it hung.");
        return Finish(CouldNotRun);
    }

    var (state, problem) = EnsureSupabase();
    // A fresh stack and CI's stack already have this checkout's migrations; anything else may have another worktree's.
    if (problem is null && state == "running" && !noReset && Environment.GetEnvironmentVariable("CI") is not { Length: > 0 })
        (state, problem) = ResetDatabase();
    var queued = waited > TimeSpan.FromSeconds(1);
    summary.Add(Row("supabase", queued ? $"{state} (waited for the lock)" : state, queued ? waited : null));
    if (problem is not null)
    {
        details.Add(problem);
        return Finish(state is "couldn't start" or "reset failed" ? Failed : CouldNotRun);
    }
    env["REQUIRE_SUPABASE"] = "true";
}

// 4. Test suites. All of them run even after a failure, so one run reports everything.
if (suites.Any(s => s.Id.StartsWith("e2e")) && Directory.Exists(e2eArtifacts)) Directory.Delete(e2eArtifacts, recursive: true);

foreach (var suite in suites)
{
    Progress(suite.Name);
    var suiteClock = Stopwatch.StartNew();
    var resultsDir = Path.Combine(outDir, suite.Id);
    var log = Path.Combine(outDir, suite.Id + ".log");
    string[] coverageArgs = coverage && suite.Coverage
        ? ["--coverage", "--coverage-output-format", "cobertura", "--coverage-output", suite.Id + ".cobertura.xml"]
        : [];
    var exit = Run("dotnet",
        ["test", "--project", suite.Project, "--no-build", "--no-progress",
         "--results-directory", resultsDir, "--report-xunit-trx", "--report-xunit-trx-filename", suite.Id + ".trx",
         .. coverageArgs, .. suite.Filter],
        log, env);

    var trx = Directory.Exists(resultsDir) ? Directory.GetFiles(resultsDir, "*.trx", SearchOption.AllDirectories).FirstOrDefault() : null;
    if (trx is null)
    {
        failed = true;
        summary.Add(Row(suite.Name, $"CRASHED (exit {exit}, no results)", suiteClock.Elapsed));
        details.Add($"{suite.Name}: the test run produced no results. Last lines of {Relative(log)}:");
        details.AddRange(Tail(log, 15).Select(l => "  " + l));
        continue;
    }

    var result = ReadTrx(trx);
    var suiteFailed = result.Failed > 0 || result.Total == 0 || exit != 0 && exit != 8;
    failed |= suiteFailed;

    var counts = $"{result.Passed} passed" + (result.Failed > 0 ? $", {result.Failed} failed" : "") + (result.Skipped > 0 ? $", {result.Skipped} skipped" : "");
    if (result.Total == 0) counts = "FAILED: no tests ran";
    else if (suiteFailed && result.Failed == 0) counts += $" (exit {exit}, see {Relative(log)})";
    summary.Add(Row(suite.Name, counts, suiteClock.Elapsed));

    foreach (var (test, message) in result.Failures)
    {
        details.Add($"FAILED {test}");
        details.AddRange(message.Take(4).Select(l => "    " + l));
        var artifacts = FindE2EArtifacts(test);
        if (artifacts is not null)
            details.Add($"    artifacts: {Relative(artifacts)} (screenshot.png, console.log, trace.zip)");
    }
    foreach (var (test, reason) in result.SkippedTests.Take(5))
        details.Add($"SKIPPED {test}: {reason}");
}

// 5. pgTAP, local and full.
if (level is "local" or "full" && Selected("db"))
{
    Progress("database (pgTAP)");
    var dbClock = Stopwatch.StartNew();
    var dbLog = Path.Combine(outDir, "db.log");
    var exit = Run("supabase", ["test", "db"], dbLog);
    var lines = File.ReadAllLines(dbLog);
    var tests = lines.Select(l => Regex.Match(l, @"Files=\d+, Tests=(\d+)")).FirstOrDefault(m => m.Success)?.Groups[1].Value;
    if (exit == 0)
        summary.Add(Row("database (pgTAP)", tests is null ? "passed" : $"{tests} passed", dbClock.Elapsed));
    else
    {
        failed = true;
        summary.Add(Row("database (pgTAP)", $"FAILED (exit {exit})", dbClock.Elapsed));
        var notOk = lines.Where(l => l.TrimStart().StartsWith("not ok") || l.Contains("Failed test")).Take(15).ToList();
        details.Add($"pgTAP failures (full output in {Relative(dbLog)}):");
        details.AddRange((notOk.Count > 0 ? notOk : Tail(dbLog, 15)).Select(l => "  " + l.Trim()));
    }
}

return Finish(failed ? Failed : Passed);

// ---------------------------------------------------------------------------------------------------------------

int Finish(int exitCode)
{
    supabaseLock?.Dispose();
    var verdict = exitCode switch { Passed => "PASSED", Failed => "FAILED", _ => "COULD NOT RUN" };
    var scope = only is null ? level : $"{level} ({string.Join(", ", steps.Where(Selected))})";
    var report = new List<string> { $"verify {scope}: {verdict} in {Duration(clock.Elapsed)}" };
    report.AddRange(summary);
    if (details.Count > 0) report.AddRange(["", .. details]);

    Console.WriteLine();
    report.ForEach(Console.WriteLine);
    Console.WriteLine();
    Console.WriteLine($"Full logs: {Relative(outDir)}/");

    if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } jobSummary)
        File.AppendAllLines(jobSummary, ["```", .. report, "```", ""]);
    // Every run's verdict in one file, so CI can print them all as the last lines of a failed job's log (that's what
    // a log tail shows; the job summary isn't readable from the log).
    File.AppendAllLines(Path.Combine(outDir, "verdicts.log"), [.. report, ""]);
    return exitCode;
}

// The checks behind the docs step. Each problem is one line naming the doc and what's missing.
List<string> CheckDocs()
{
    const string FeaturesDoc = "docs/agents/FEATURES.md", TestingDoc = "docs/agents/TESTING.md";
    const string SharedFeatures = "MealWiz/MealWiz/MealWiz.Shared/Features";
    var problems = new List<string>();
    string ReadDoc(string relative) => File.Exists(Path.Combine(root, relative)) ? File.ReadAllText(Path.Combine(root, relative)) : "";
    // A whole name: "meal_plans" doesn't count as named inside "meal_plans-meals".
    static bool Names(string text, string name) => Regex.IsMatch(text, $@"(?<![\w-]){Regex.Escape(name)}(?![\w-])");

    var features = ReadDoc(FeaturesDoc);
    var headings = string.Join('\n', features.Split('\n').Where(l => l.StartsWith('#')));
    foreach (var parent in new[] { SharedFeatures, "MealWiz/src/MealWiz.Functions/Functions" })
        foreach (var folder in Directory.GetDirectories(Path.Combine(root, parent)).Select(Path.GetFileName))
            if (!Names(headings, folder!))
                problems.Add($"{FeaturesDoc}: no heading for {parent}/{folder}/");

    foreach (var file in Directory.GetFiles(Path.Combine(root, SharedFeatures), "*.cs", SearchOption.AllDirectories))
        if (File.ReadAllText(file).Contains("IRequestHandler<") && !Names(features, Path.GetFileNameWithoutExtension(file)))
            problems.Add($"{FeaturesDoc}: handler {Path.GetFileNameWithoutExtension(file)} isn't listed ({Relative(file)})");

    // Tables still standing after every migration, in order.
    var tables = new SortedSet<string>();
    foreach (var migration in Directory.GetFiles(Path.Combine(root, "supabase", "migrations"), "*.sql").Order())
        foreach (Match m in Regex.Matches(File.ReadAllText(migration),
                     @"\b(create|drop)\s+table\s+(?:if\s+(?:not\s+)?exists\s+)?(?:""?public""?\.)?""?([\w-]+)""?", RegexOptions.IgnoreCase))
            if (m.Groups[1].Value.Equals("create", StringComparison.OrdinalIgnoreCase)) tables.Add(m.Groups[2].Value);
            else tables.Remove(m.Groups[2].Value);
    problems.AddRange(tables.Where(t => !Names(features, t)).Select(t => $"{FeaturesDoc}: table {t} isn't listed"));

    var testing = ReadDoc(TestingDoc);
    problems.AddRange(steps.Union(["e2e-smoke", "e2e", "db"]).Where(s => !testing.Contains($"`{s}`"))
        .Select(s => $"{TestingDoc}: verify step `{s}` isn't described"));

    // A path in backticks resolves from the repo root or from a project folder, so `Features/Meals/` works too.
    // Skipped: placeholders and globs (<>*{}…), routes (/meals), URLs, and names with no slash or no extension.
    var bases = Directory.GetFiles(Path.Combine(root, "MealWiz"), "*.csproj", SearchOption.AllDirectories)
        .Select(p => Path.GetDirectoryName(p)!).Prepend(root).ToList();
    var docs = Directory.GetFiles(Path.Combine(root, "docs", "agents"), "*.md").Prepend(Path.Combine(root, "CLAUDE.md"));
    foreach (var doc in docs.Where(File.Exists))
    {
        var text = Regex.Replace(File.ReadAllText(doc), @"(?ms)^\s*```.*?^\s*```", "");
        foreach (var path in Regex.Matches(text, @"`([^`\s]+)`").Select(m => m.Groups[1].Value).Distinct())
        {
            if (!path.Contains('/') || path.StartsWith('/') || path.IndexOfAny("<>*{}…:$|=()".ToCharArray()) >= 0) continue;
            if (!path.EndsWith('/') && !path.Split('/').Last().Contains('.')) continue;
            if (!bases.Any(b => File.Exists(Path.Combine(b, path)) || Directory.Exists(Path.Combine(b, path))))
                problems.Add($"{Relative(doc)}: `{path}` doesn't exist");
        }
    }
    return problems;
}

static void Progress(string step) => Console.WriteLine($"[verify] {step}...");

static string Row(string name, string result, TimeSpan? took = null) =>
    $"  {name,-24}{result}{(took is { } t ? $"  ({Duration(t)})" : "")}";

static string Duration(TimeSpan t) => t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}m{t.Seconds:00}s" : $"{t.TotalSeconds:0}s";

string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

static string? FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "supabase", "config.toml")))
            return dir.FullName;
    return null;
}

// Runs a command with its output appended to logPath. Returns the exit code, or -1 if it couldn't start.
int Run(string fileName, string[] arguments, string logPath, IDictionary<string, string>? extraEnv = null)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = root,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    // `dotnet run` leaves MSBuild variables in this process's environment that would point child builds at the
    // wrong SDK.
    foreach (var key in startInfo.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
        startInfo.Environment.Remove(key);
    foreach (var (key, value) in extraEnv ?? new Dictionary<string, string>())
        startInfo.Environment[key] = value;

    using var writer = new StreamWriter(logPath, append: true, Encoding.UTF8);
    writer.WriteLine($"> {fileName} {string.Join(' ', arguments)}");
    writer.Flush();

    Process? process;
    try
    {
        process = Process.Start(startInfo);
    }
    catch (Exception e)
    {
        writer.WriteLine($"Couldn't start {fileName}: {e.Message}");
        return -1;
    }
    if (process is null) return -1;

    using (process)
    {
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (writer) writer.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (writer) writer.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        writer.WriteLine();
        return process.ExitCode;
    }
}

// Is local Supabase up? Starts it if not. Returns the state for the summary and, if it can't be used, why.
(string State, string? Problem) EnsureSupabase()
{
    var log = Path.Combine(outDir, "supabase.log");
    if (Run("supabase", ["--version"], log) == -1)
        return ("missing", "The Supabase CLI isn't on PATH. Install it (https://supabase.com/docs/guides/local-development/cli/getting-started) or run `verify fast`.");

    if (Run("supabase", ["status", "-o", "env"], log) == 0 && File.ReadAllText(log).Contains("API_URL="))
        return ("running", null);

    if (Run("docker", ["info"], log) != 0 && !StartDockerDaemon(log))
        return ("no Docker", "Docker isn't running, and local, smoke and full need the local Supabase stack. Start Docker Desktop, or run `verify fast`.");

    // The same services CI leaves out. Studio (and postgres-meta, which it needs) only with --studio: no test uses
    // them, and they're a large part of the first image pull.
    Progress("supabase start (first start pulls images and can take a few minutes)");
    var excluded = "imgproxy,edge-runtime,storage-api,logflare,vector,supavisor" + (studio ? "" : ",studio,postgres-meta");
    if (Run("supabase", ["start", "-x", excluded], log) == 0)
        return ("started", null);

    return ("couldn't start", $"`supabase start` failed (a broken migration or seed fails here too). Last lines of {Relative(log)}:\n"
        + string.Join('\n', Tail(log, 15).Select(l => "  " + l)));
}

// On Linux (a cloud container, where nothing restarts the daemon if it dies), starts dockerd when it's installed and
// this runs as root. Returns whether Docker answers. Elsewhere it's Docker Desktop's job, so it returns false.
bool StartDockerDaemon(string log)
{
    if (!OperatingSystem.IsLinux() || Environment.UserName != "root" || Environment.GetEnvironmentVariable("CI") is { Length: > 0 })
        return false;
    Progress("docker isn't running: starting dockerd");
    try
    {
        var daemonLog = Path.Combine(Path.GetTempPath(), "mealwiz-dockerd.log");
        Process.Start(new ProcessStartInfo("bash", ["-c", $"nohup dockerd > '{daemonLog}' 2>&1 &"]) { UseShellExecute = false })?.WaitForExit();
    }
    catch (Exception e)
    {
        File.AppendAllText(log, $"Couldn't start dockerd: {e.Message}\n");
        return false;
    }
    for (var i = 0; i < 30; i++)
    {
        if (Run("docker", ["info"], log) == 0) return true;
        Thread.Sleep(TimeSpan.FromSeconds(1));
    }
    return false;
}

// Waits (up to lockTimeout) for the lock on local Supabase and sets supabaseLock. FileShare.None is an exclusive
// flock on Linux and macOS too. Who holds it goes in a separate file, since the lock file itself can't be read.
TimeSpan WaitForSupabaseLock()
{
    var waitClock = Stopwatch.StartNew();
    var announced = false;
    while (waitClock.Elapsed < lockTimeout)
    {
        try
        {
            supabaseLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            File.WriteAllText(lockPath + ".owner",
                $"{root} (verify {level}, pid {Environment.ProcessId}, since {DateTime.Now:HH:mm:ss})");
            return waitClock.Elapsed;
        }
        catch (IOException)
        {
            if (!announced)
                Progress($"supabase: waiting for another verify to finish with local Supabase: {LockOwner()}");
            announced = true;
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
    return waitClock.Elapsed;
}

string LockOwner()
{
    try { return File.ReadAllText(lockPath + ".owner").Trim(); }
    catch (IOException) { return "unknown"; }
}

// Puts this worktree's schema and seed.sql in the shared database. Only called with the lock held.
(string State, string? Problem) ResetDatabase()
{
    Progress("supabase db reset (this worktree's migrations and seed)");
    var log = Path.Combine(outDir, "supabase.log");
    if (Run("supabase", ["db", "reset"], log) == 0)
        return ("running, reset", null);
    // A CLI installed without its supabase-go binary can start a stack but not reset it (MEA-72; scripts/cloud-setup.sh
    // installs both). A fresh start applies the migrations and seed too, so restart instead.
    if (File.ReadAllText(log).Contains("supabase-go"))
    {
        Progress("supabase db reset needs supabase-go, which isn't installed: restarting the stack instead");
        Run("supabase", ["stop", "--no-backup"], log);
        var (state, problem) = EnsureSupabase();
        return (state == "started" ? "restarted (no supabase-go)" : state, problem);
    }
    return ("reset failed", $"`supabase db reset` failed (a broken migration or seed fails here). Last lines of {Relative(log)}:\n"
        + string.Join('\n', Tail(log, 15).Select(l => "  " + l)));
}

IEnumerable<string> BuildErrors(string buildLog)
{
    // "C:\...\MealsPage.razor(12,5): error CS1002: ; expected [C:\...\MealWiz.Shared.csproj]"
    var errors = File.ReadLines(buildLog)
        .Where(l => Regex.IsMatch(l, @":\s*error\s+[A-Z]+\d*\s*:"))
        .Select(l => Regex.Replace(l.Trim(), @"\s*\[[^\]]+\.csproj(::[^\]]*)?\]$", ""))
        .Select(l => l.Replace(root! + Path.DirectorySeparatorChar, "", StringComparison.OrdinalIgnoreCase))
        .Distinct()
        .ToList();
    return errors.Count == 0 ? Tail(buildLog, 20) : errors.Take(20);
}

static List<string> Tail(string path, int count) =>
    File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Trim().Length > 0).TakeLast(count).ToList() : [];

static TrxResult ReadTrx(string path)
{
    XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    var doc = XDocument.Load(path);
    var results = doc.Descendants(ns + "UnitTestResult").ToList();

    var failures = results
        .Where(r => (string?)r.Attribute("outcome") is "Failed" or "Error" or "Timeout" or "Aborted")
        .Select(r =>
        {
            var message = (string?)r.Element(ns + "Output")?.Element(ns + "ErrorInfo")?.Element(ns + "Message") ?? "(no message)";
            var lines = message.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0).ToList();
            return (ShortName((string?)r.Attribute("testName") ?? "?"), lines);
        })
        .ToList();

    var skipped = results
        .Where(r => (string?)r.Attribute("outcome") is "NotExecuted")
        .Select(r =>
        {
            var reason = (string?)r.Element(ns + "Output")?.Element(ns + "StdOut")
                ?? (string?)r.Element(ns + "Output")?.Element(ns + "ErrorInfo")?.Element(ns + "Message") ?? "";
            return (ShortName((string?)r.Attribute("testName") ?? "?"), reason.Split('\n')[0].Trim());
        })
        .ToList();

    var passed = results.Count(r => (string?)r.Attribute("outcome") is "Passed");
    return new TrxResult(results.Count, passed, failures.Count, skipped.Count, failures, skipped);
}

// "MauiWASM.Tests.E2ETests.SmokeTests.Page_Renders(page: ...)" -> "SmokeTests.Page_Renders(page: ...)"
static string ShortName(string testName)
{
    var paren = testName.IndexOf('(');
    var head = paren < 0 ? testName : testName[..paren];
    var parts = head.Split('.');
    var shortHead = parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : head;
    return shortHead + (paren < 0 ? "" : testName[paren..]);
}

// E2ETestBase names the folder "<Class>.<Method>", plus the arguments for a theory case.
string? FindE2EArtifacts(string shortTestName)
{
    if (!Directory.Exists(e2eArtifacts)) return null;
    var paren = shortTestName.IndexOf('(');
    var method = paren < 0 ? shortTestName : shortTestName[..paren];
    var candidates = Directory.GetDirectories(e2eArtifacts)
        .Where(d => Path.GetFileName(d) == method || Path.GetFileName(d).StartsWith(method + "(", StringComparison.Ordinal))
        .ToList();
    if (candidates.Count <= 1) return candidates.FirstOrDefault();

    // Several cases of one theory failed: match on the argument values.
    var values = Regex.Matches(shortTestName, "\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();
    return candidates.FirstOrDefault(d => values.All(v => Path.GetFileName(d).Contains(Sanitize(v)))) ?? candidates[0];
}

static string Sanitize(string value) => string.Join("_", value.Split(Path.GetInvalidFileNameChars()));

record TrxResult(int Total, int Passed, int Failed, int Skipped, List<(string Test, List<string> Message)> Failures, List<(string Test, string Reason)> SkippedTests);
