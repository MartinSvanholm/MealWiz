namespace MauiWASM.Tests.TestInfrastructure.E2E;

internal static class RepoPaths
{
    /// <summary>The directory holding supabase/config.toml, found by walking up from the test binaries.</summary>
    public static string? FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "supabase", "config.toml")))
                return directory.FullName;
        }

        return null;
    }
}
