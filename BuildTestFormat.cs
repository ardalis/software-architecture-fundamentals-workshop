#!/usr/bin/env dotnet
// Builds, tests, and checks formatting for every .NET solution under samples/.
//
//   dotnet BuildTestFormat.cs                 all samples
//   dotnet BuildTestFormat.cs epic-saga       only samples whose folder name contains "epic-saga"
//   dotnet BuildTestFormat.cs --no-test       skip the tests (they need Docker)
//
// Exits with a non-zero code if any step fails for any sample.
using System.Diagnostics;

var skipTests = args.Contains("--no-test");
var filters = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var samplesDir = Path.Combine(repoRoot, "samples");

var solutions = Directory.EnumerateFiles(samplesDir, "*.slnx", SearchOption.AllDirectories)
    .Where(s => !s.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
             && !s.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
    .Where(s => filters.Length == 0 || filters.Any(f => SampleName(s).Contains(f, StringComparison.OrdinalIgnoreCase)))
    .OrderBy(s => s, StringComparer.Ordinal)
    .ToList();

if (solutions.Count == 0)
{
    Console.Error.WriteLine("No solutions found under samples/.");
    return 1;
}

var results = new List<(string Solution, string Step, bool Passed, TimeSpan Elapsed)>();

foreach (var solution in solutions)
{
    var name = Path.GetRelativePath(repoRoot, solution);
    // Run from the solution's folder so samples/global.json picks the SDK and test runner.
    var workingDir = Path.GetDirectoryName(solution)!;

    var steps = new List<(string Step, string Arguments)>
    {
        ("build", $"build \"{solution}\" --nologo"),
    };
    if (!skipTests)
    {
        steps.Add(("test", $"test --solution \"{solution}\" --no-build"));
    }
    steps.Add(("format", $"format \"{solution}\" --verify-no-changes --no-restore"));

    foreach (var (step, arguments) in steps)
    {
        Console.WriteLine();
        Console.WriteLine($"==> {name}: {step}");
        var stopwatch = Stopwatch.StartNew();
        var passed = await RunAsync("dotnet", arguments, workingDir) == 0;
        results.Add((name, step, passed, stopwatch.Elapsed));

        if (!passed)
        {
            Console.Error.WriteLine($"FAILED: {name}: {step}");
            break; // No point testing or formatting a solution that does not build.
        }
    }
}

Console.WriteLine();
Console.WriteLine("Summary");
foreach (var (solution, step, passed, elapsed) in results)
{
    Console.WriteLine($"  {(passed ? "PASS" : "FAIL"),-4}  {step,-6}  {elapsed.TotalSeconds,6:0}s  {solution}");
}

var failed = results.Count(r => !r.Passed);
Console.WriteLine(failed == 0 ? "All steps passed." : $"{failed} step(s) failed.");
return failed == 0 ? 0 : 1;

// The sample's folder name, e.g. "epic-saga" for samples/epic-saga/EpicSaga.slnx.
static string SampleName(string solution) => Path.GetFileName(Path.GetDirectoryName(solution)!);

static async Task<int> RunAsync(string fileName, string arguments, string workingDirectory)
{
    using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
    })!;
    await process.WaitForExitAsync();
    return process.ExitCode;
}

static string FindRepoRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "samples")) && File.Exists(Path.Combine(dir.FullName, "BuildTestFormat.cs")))
        {
            return dir.FullName;
        }
    }

    throw new InvalidOperationException("Run this script from inside the repository.");
}
