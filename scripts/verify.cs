// One command that tells you (or an AI agent) whether the app still works. Run it through the shims:
//
//   scripts/verify.ps1 <fast|smoke|full>     (Windows)
//   scripts/verify.sh  <fast|smoke|full>     (Linux, macOS, Git Bash)
//
// They run this file as a .NET 10 file-based app (`dotnet run scripts/verify.cs -- <level>`), so both shells share
// one implementation. Each level includes the one before it:
//
//   fast   build + unit and bUnit tests. No Docker needed.
//   smoke  + make sure local Supabase is running (start it if not) + integration tests + the Smoke E2E tests.
//   full   + every E2E journey + the pgTAP database tests (`supabase test db`).
//
// The exit code is the verdict: 0 all passed, 1 something failed, 2 couldn't run (bad arguments, no Docker...).
// Only a short summary is printed. Full build and test logs, and a TRX per suite, go to artifacts/verify/<level>/.
//
// CI runs `full` in pieces, so each test project shows up as its own step in the PR check, and the suite list stays
// in this one file:
//
//   --only <steps>     run only these steps of the level (comma list: build, shared-unit, ..., db; see --help).
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

          fast   build + unit and bUnit tests (no Docker)
          smoke  fast + local Supabase (started if needed) + integration tests + Smoke E2E tests
          full   smoke + every E2E journey + pgTAP database tests

        Exit code 0 = passed, 1 = failed, 2 = couldn't run. Full logs: artifacts/verify/<level>/

        For CI (see the top of scripts/verify.cs):
          --only <steps>     comma list of steps to run: build, shared-unit, functions-unit, web-unit,
                             shared-integration, functions-integration, e2e-smoke (smoke), e2e (full), db (full)
          --coverage         Cobertura coverage for the unit and integration suites
          --check-complete   fail if a step of the level hasn't run
        """);
    return level is null ? CouldNotRun : Passed;
}
if (level is not ("fast" or "smoke" or "full"))
{
    Console.WriteLine($"Unknown level '{args[0]}'. Use fast, smoke or full.");
    return CouldNotRun;
}

HashSet<string>? only = null;
var coverage = false;
var checkComplete = false;
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
    suites.Add(level is "smoke"
        ? ("e2e-smoke", "E2E smoke", WebTests, ["--filter-trait", "Category=Smoke"], false)
        : ("e2e", "E2E (all journeys)", WebTests, ["--filter-trait", "Category=E2E"], false));
}

// Every step of the level, in order, as --only names them.
var steps = new List<string> { "build" };
steps.AddRange(suites.Select(s => s.Id));
if (level is "full") steps.Add("db");

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

// 1. Build. The MAUI head in MealWiz.sln needs mobile workloads, so build what the tests reference instead
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

// 2. Supabase, for the steps that use it: integration, E2E and pgTAP. REQUIRE_SUPABASE makes the fixtures fail
// instead of skipping, so a broken stack can't pass as "all skipped".
var env = new Dictionary<string, string>();
if (suites.Any(s => !s.Id.EndsWith("-unit")) || Selected("db") && level is "full")
{
    Progress("supabase");
    var (state, problem) = EnsureSupabase();
    summary.Add(Row("supabase", state));
    if (problem is not null)
    {
        details.Add(problem);
        return Finish(state == "couldn't start" ? Failed : CouldNotRun);
    }
    env["REQUIRE_SUPABASE"] = "true";
}

// 3. Test suites. All of them run even after a failure, so one run reports everything.
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

// 4. pgTAP, full only.
if (level is "full" && Selected("db"))
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
    return exitCode;
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

    if (Run("docker", ["info"], log) != 0)
        return ("no Docker", "Docker isn't running, and smoke and full need the local Supabase stack. Start Docker Desktop, or run `verify fast`.");

    // The same services as CI leaves out, except Studio (and postgres-meta, which Studio needs), so a developer
    // can still look at the data.
    Progress("supabase start (first start pulls images and can take a few minutes)");
    if (Run("supabase", ["start", "-x", "imgproxy,edge-runtime,storage-api,logflare,vector,supavisor,mailpit"], log) == 0)
        return ("started", null);

    return ("couldn't start", $"`supabase start` failed (a broken migration or seed fails here too). Last lines of {Relative(log)}:\n"
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
