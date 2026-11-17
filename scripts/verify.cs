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
//          `--quick` is for iterating: it skips the database reset when the stack already holds this worktree's
//          migrations and seed, and skips the Functions and pgTAP steps when nothing they test differs from
//          origin/dev. Plain `local` (no --quick) is still the check before a PR.
//   smoke  fast + local Supabase + integration tests + the Smoke E2E tests.
//   full   smoke + every E2E journey + the pgTAP database tests.
//
// Every level that uses Supabase takes a machine-wide lock first, so parallel worktrees (agent sessions) take turns
// on the one local stack instead of slowing each other down and changing its schema under each other's tests. Under
// the lock it resets the database to this worktree's migrations and seed.sql (`supabase db reset`), unless the stack
// was just started, CI is set, or --no-reset is passed; a reset that fails restarts the stack instead. It starts the
// stack without Studio (--studio adds it) and, on Linux as root (a cloud container), starts Docker through
// scripts/start-docker.sh if it isn't running. The build runs while it waits for the lock and starts the stack, and
// the unit suites run side by side.
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
          --quick            local only, for iterating: no reset when the stack already has this worktree's
                             migrations and seed; no Functions or pgTAP steps when nothing they test changed
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
var quick = false;
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
        case "--quick" when level is "local":
            quick = true;
            break;
        case "--quick":
            Console.WriteLine("--quick only goes with `local`.");
            return CouldNotRun;
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
// The three test projects, as one thing to build.
const string TestsFilter = "MealWiz/MealWiz.Tests.slnf";
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

// --quick: steps whose code is the same as on origin/dev, with the reason, for the summary. A step named in --only
// always runs.
var quickSkipped = new Dictionary<string, string>();
if (quick && only is null && ChangedSinceDev() is { } changed)
{
    if (!changed.Any(f => f.StartsWith("MealWiz/src/MealWiz.Functions/") || f.StartsWith("MealWiz/tests/MealWiz.Functions.Tests/")
                          || f.StartsWith("MealWiz/tests/Contracts/")))
        foreach (var step in new[] { "functions-unit", "functions-integration" })
            quickSkipped[step] = "skipped (--quick: no Functions change since origin/dev)";
    if (!changed.Any(f => f.StartsWith("supabase/")))
        quickSkipped["db"] = "skipped (--quick: no supabase/ change since origin/dev)";
    suites.RemoveAll(s => quickSkipped.ContainsKey(s.Id));
}

bool Selected(string step) => (only is null || only.Contains(step)) && !quickSkipped.ContainsKey(step);

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
// Which migrations and seed the shared stack holds, written after each reset or fresh start, for --quick.
var schemaMarker = Path.Combine(Path.GetTempPath(), "mealwiz-verify-supabase.schema");

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

// 2. Build, in the background while step 3 gets Supabase ready: a cold build and a first `supabase start` each take
// a minute or more, and a run that has to wait for the lock builds meanwhile.
var build = Selected("build") ? Task.Run(Build) : null;

// 3. Supabase, for the steps that use it: integration, E2E and pgTAP. REQUIRE_SUPABASE makes the fixtures fail
// instead of skipping, so a broken stack can't pass as "all skipped".
// All worktrees share one local stack, so the lock is held from here until the run ends.
var env = new Dictionary<string, string>();
string? supabaseRow = null, supabaseProblem = null;
var supabaseExit = CouldNotRun;
if (suites.Any(s => !s.Id.EndsWith("-unit")) || Selected("db") && level is "local" or "full")
{
    Progress("supabase");
    var waited = WaitForSupabaseLock();
    if (supabaseLock is null && !BuildFailed())
    {
        supabaseRow = Row("supabase", "locked", waited);
        supabaseProblem = $"Another verify held local Supabase for over {lockTimeout.TotalMinutes:0} minutes ({LockOwner()}). " +
                          "Wait for it, or stop that process if it hung.";
    }
    else if (!BuildFailed())
    {
        var (state, problem) = EnsureSupabase();
        // A fresh stack and CI's stack already have this checkout's migrations; anything else may have another worktree's.
        if (problem is null && state == "running" && quick && File.Exists(schemaMarker) && File.ReadAllText(schemaMarker) == SchemaFingerprint())
            state = "running, reset skipped (--quick: same migrations and seed)";
        else if (problem is null && state == "running" && !noReset && Environment.GetEnvironmentVariable("CI") is not { Length: > 0 })
            (state, problem) = ResetDatabase();
        // A reset restarts the auth and API containers, and once in a while GoTrue doesn't come back (502 on every
        // auth call, so every integration test fails in its fixture). Check both answer, and restart the stack once
        // if they don't.
        if (problem is null && !ApiAnswers())
        {
            Progress("the Supabase API isn't answering: restarting the stack");
            Run("supabase", ["stop", "--no-backup"], Path.Combine(outDir, "supabase.log"));
            (state, problem) = EnsureSupabase();
            if (problem is null && !ApiAnswers())
                (state, problem) = ("couldn't start", "The Supabase API (auth or REST on 127.0.0.1:54321) didn't answer after a restart. See supabase.log.");
            else if (problem is null)
                state = "restarted (API didn't answer)";
        }
        // The stack now holds this worktree's migrations and seed, so a --quick run can skip the next reset.
        if (problem is null && (state.StartsWith("started") || state.StartsWith("restarted") || state == "running, reset"))
            File.WriteAllText(schemaMarker, SchemaFingerprint());
        var queued = waited > TimeSpan.FromSeconds(1);
        supabaseRow = Row("supabase", queued ? $"{state} (waited for the lock)" : state, queued ? waited : null);
        supabaseProblem = problem;
        if (state is "couldn't start") supabaseExit = Failed;
        env["REQUIRE_SUPABASE"] = "true";
    }
}

if (build is not null)
{
    var (ok, row, errors) = build.Result;
    summary.Add(row);
    details.AddRange(errors);
    if (!ok) return Finish(Failed);
}
if (supabaseRow is not null) summary.Add(supabaseRow);
if (supabaseProblem is not null)
{
    details.Add(supabaseProblem);
    return Finish(supabaseExit);
}

// 4. Test suites. All of them run even after a failure, so one run reports everything.
if (suites.Any(s => s.Id.StartsWith("e2e")) && Directory.Exists(e2eArtifacts)) Directory.Delete(e2eArtifacts, recursive: true);

// The unit suites touch nothing shared, so they run side by side. The rest take turns on the Supabase stack.
var unitRuns = suites.Where(s => s.Id.EndsWith("-unit")).Select(s => Task.Run(() => RunSuite(s))).ToList();
foreach (var result in unitRuns.Select(r => r.Result).Concat(suites.Where(s => !s.Id.EndsWith("-unit")).Select(RunSuite)))
{
    failed |= result.Failed;
    summary.Add(result.Row);
    details.AddRange(result.Details);
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

// Whether local Supabase's auth and REST APIs both answer 200, waiting up to 30 seconds for them.
bool ApiAnswers()
{
    using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
    for (var i = 0; i < 30; i++)
    {
        try
        {
            if (http.GetAsync("http://127.0.0.1:54321/auth/v1/health").Result.IsSuccessStatusCode
                && http.GetAsync("http://127.0.0.1:54321/rest/v1/").Result.IsSuccessStatusCode)
                return true;
        }
        catch (AggregateException) { }
        Thread.Sleep(TimeSpan.FromSeconds(1));
    }
    return false;
}

// Files that differ from origin/dev: committed since the merge base, changed in the working tree, or untracked.
// Null if git can't tell (no origin/dev, say), and then --quick skips nothing.
List<string>? ChangedSinceDev()
{
    var log = Path.Combine(outDir, "quick.log");
    var baseLog = Path.Combine(outDir, "quick-base.log");
    File.Delete(baseLog);
    if (Run("git", ["merge-base", "HEAD", "origin/dev"], baseLog) != 0) return null;
    var mergeBase = File.ReadLines(baseLog).Skip(1).FirstOrDefault(l => Regex.IsMatch(l, "^[0-9a-f]{40}$"));
    if (mergeBase is null) return null;
    File.Delete(log);
    if (Run("git", ["diff", "--name-only", mergeBase], log) != 0) return null;
    if (Run("git", ["ls-files", "--others", "--exclude-standard"], log) != 0) return null;
    return File.ReadLines(log).Where(l => l.Length > 0 && !l.StartsWith("> ")).Select(l => l.Replace('\\', '/')).ToList();
}

// The migrations and seed.sql of this worktree, plus when the database container started (a stack started or reset
// some other way has another start time), as one string.
string SchemaFingerprint()
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    var files = Directory.GetFiles(Path.Combine(root, "supabase", "migrations"), "*.sql").Order(StringComparer.Ordinal)
        .Append(Path.Combine(root, "supabase", "seed.sql")).Where(File.Exists);
    foreach (var file in files)
    {
        var name = Encoding.UTF8.GetBytes(Path.GetFileName(file) + "\n");
        sha.TransformBlock(name, 0, name.Length, null, 0);
        var content = File.ReadAllBytes(file);
        sha.TransformBlock(content, 0, content.Length, null, 0);
    }
    sha.TransformFinalBlock([], 0, 0);

    var project = Regex.Match(File.ReadAllText(Path.Combine(root, "supabase", "config.toml")), @"(?m)^project_id\s*=\s*""([^""]+)""").Groups[1].Value;
    var inspectLog = Path.Combine(outDir, "quick-db.log");
    File.Delete(inspectLog);
    Run("docker", ["inspect", "-f", "{{.State.StartedAt}}", $"supabase_db_{project}"], inspectLog);
    var startedAt = File.ReadLines(inspectLog).Skip(1).FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
    return $"{Convert.ToHexString(sha.Hash!)} {startedAt}";
}

// The MAUI head in MealWiz.sln needs mobile workloads, so this builds what the tests reference instead (MealWiz.Shared,
// MauiWASM, MealWiz.Functions and the three test projects), like CI: one build of the solution filter that lists
// the test projects, so MSBuild builds them in parallel and evaluates the shared projects once.
(bool Ok, string Row, List<string> Errors) Build()
{
    Progress("build");
    var buildClock = Stopwatch.StartNew();
    var buildLog = Path.Combine(outDir, "build.log");
    if (Run("dotnet", ["build", TestsFilter, "-nologo", "-v:minimal", "-clp:NoSummary"], buildLog) == 0)
        return (true, Row("build", "ok", buildClock.Elapsed), []);
    return (false, Row("build", "FAILED", buildClock.Elapsed), ["Build errors:", .. BuildErrors(buildLog).Select(e => "  " + e)]);
}

// True once the background build has failed: there is nothing to run then, so don't wait for or reset Supabase.
bool BuildFailed() => build is { IsCompleted: true, Result.Ok: false };

// Runs one test suite and reads its TRX. A suite that fails doesn't stop the others, so one run reports everything.
(bool Failed, string Row, List<string> Details) RunSuite((string Id, string Name, string Project, string[] Filter, bool Coverage) suite)
{
    Progress(suite.Name);
    var suiteDetails = new List<string>();
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
        suiteDetails.Add($"{suite.Name}: the test run produced no results. Last lines of {Relative(log)}:");
        suiteDetails.AddRange(Tail(log, 15).Select(l => "  " + l));
        return (true, Row(suite.Name, $"CRASHED (exit {exit}, no results)", suiteClock.Elapsed), suiteDetails);
    }

    var result = ReadTrx(trx);
    var suiteFailed = result.Failed > 0 || result.Total == 0 || exit != 0 && exit != 8;

    var counts = $"{result.Passed} passed" + (result.Failed > 0 ? $", {result.Failed} failed" : "") + (result.Skipped > 0 ? $", {result.Skipped} skipped" : "");
    if (result.Total == 0) counts = "FAILED: no tests ran";
    else if (suiteFailed && result.Failed == 0) counts += $" (exit {exit}, see {Relative(log)})";

    foreach (var (test, message) in result.Failures)
    {
        suiteDetails.Add($"FAILED {test}");
        suiteDetails.AddRange(message.Take(4).Select(l => "    " + l));
        var artifacts = FindE2EArtifacts(test);
        if (artifacts is not null)
            suiteDetails.Add($"    artifacts: {Relative(artifacts)} (screenshot.png, console.log, trace.zip)");
    }
    foreach (var (test, reason) in result.SkippedTests.Take(5))
        suiteDetails.Add($"SKIPPED {test}: {reason}");
    return (suiteFailed, Row(suite.Name, counts, suiteClock.Elapsed), suiteDetails);
}

int Finish(int exitCode)
{
    supabaseLock?.Dispose();
    var verdict = exitCode switch { Passed => "PASSED", Failed => "FAILED", _ => "COULD NOT RUN" };
    var scope = only is null ? level : $"{level} ({string.Join(", ", steps.Where(Selected))})";
    var report = new List<string> { $"verify {scope}{(quick ? " --quick" : "")}: {verdict} in {Duration(clock.Elapsed)}" };
    report.AddRange(summary);
    report.AddRange(quickSkipped.Select(q => Row(q.Key, q.Value)));
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
    {
        // A stack started before Storage joined the test stacks has no Storage container. Restart it once (this wipes
        // local data; the database is reset from the migrations and seed anyway).
        var storageLog = Path.Combine(outDir, "docker-storage.log");
        if (Run("docker", ["ps", "--filter", "name=supabase_storage_", "--format", "{{.Names}}"], storageLog) != 0
            || File.ReadAllText(storageLog).Contains("supabase_storage_"))
            return ("running", null);
        Progress("the running Supabase stack has no Storage: restarting it once (local data is wiped)");
        Run("supabase", ["stop", "--no-backup"], log);
    }

    if (Run("docker", ["info"], log) != 0 && !StartDockerDaemon(log))
        return ("no Docker", "Docker isn't running, and local, smoke and full need the local Supabase stack. Start Docker Desktop, or run `verify fast`.");

    // The same services CI leaves out. Studio (and postgres-meta, which it needs) only with --studio: no test uses
    // them, and they're a large part of the first image pull.
    Progress("supabase start (first start pulls images and can take a few minutes)");
    var excluded = "imgproxy,edge-runtime,logflare,vector,supavisor" + (studio ? "" : ",studio,postgres-meta");
    if (Run("supabase", ["start", "-x", excluded], log) == 0)
        return ("started", null);
    // Containers left over from before a Docker restart, or from another worktree's run, can still be booting
    // ("container is not ready: starting", MEA-120). Start once more from a clean stop.
    Progress("supabase start failed: stopping the stack and starting it once more");
    Run("supabase", ["stop", "--no-backup"], log);
    if (Run("supabase", ["start", "-x", excluded], log) == 0)
        return ("started (second try)", null);

    return ("couldn't start", $"`supabase start` failed (a broken migration or seed fails here too). Last lines of {Relative(log)}:\n"
        + string.Join('\n', Tail(log, 15).Select(l => "  " + l)));
}

// On Linux (a cloud container, where nothing restarts the daemon if it dies), starts dockerd through
// scripts/start-docker.sh when this runs as root. Returns whether Docker answers. Elsewhere it's Docker Desktop's
// job, so it returns false.
bool StartDockerDaemon(string log)
{
    if (!OperatingSystem.IsLinux() || Environment.UserName != "root" || Environment.GetEnvironmentVariable("CI") is { Length: > 0 })
        return false;
    Progress("docker isn't running: starting dockerd");
    return Run("bash", [Path.Combine(root, "scripts", "start-docker.sh")], log) == 0;
}

// Waits (up to lockTimeout) for the lock on local Supabase and sets supabaseLock. FileShare.None is an exclusive
// flock on Linux and macOS too. Who holds it goes in a separate file, since the lock file itself can't be read.
TimeSpan WaitForSupabaseLock()
{
    var waitClock = Stopwatch.StartNew();
    var announced = false;
    while (waitClock.Elapsed < lockTimeout && !BuildFailed())
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
    // The reset can fail for reasons that aren't this worktree's: the stack is still settling after another
    // worktree's run (MEA-120), or the CLI was installed without the supabase-go binary that `db reset` needs
    // (MEA-72). A fresh start applies the migrations and seed too, so restart the stack once instead. A broken
    // migration or seed then fails `supabase start`, with its error.
    Progress("supabase db reset failed: restarting the stack instead (see supabase.log for the reset's error)");
    Run("supabase", ["stop", "--no-backup"], log);
    var (state, problem) = EnsureSupabase();
    return (state == "started" ? "restarted (db reset failed)" : state, problem);
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
