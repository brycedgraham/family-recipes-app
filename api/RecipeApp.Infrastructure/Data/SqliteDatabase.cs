namespace RecipeApp.Infrastructure.Data;

/// <summary>
/// Resolves the SQLite connection string: rewrites a relative "Data Source" path against the
/// repository root, ensures the data directory exists before the connection opens, and makes
/// sure foreign keys are enforced (SQLite leaves them off by default).
/// </summary>
public static class SqliteDatabase
{
    /// <summary>Directory containing the resolved database file. Set by <see cref="Resolve"/>;
    /// used to place sibling data (e.g. recipe images) next to the database.</summary>
    public static string? DataDirectory { get; private set; }
    public static string Resolve(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var dataSource = parts.FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));
        if (dataSource is null)
            throw new InvalidOperationException("Connection string must contain a 'Data Source' entry.");

        var path = dataSource["Data Source=".Length..].Trim();
        if (!Path.IsPathRooted(path))
        {
            var baseDir = FindRepoRoot() ?? AppContext.BaseDirectory;
            path = Path.GetFullPath(Path.Combine(baseDir, path));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        DataDirectory = Path.GetDirectoryName(path)!;

        var hasForeignKeys = parts.Any(p => p.StartsWith("Foreign Keys=", StringComparison.OrdinalIgnoreCase));
        var result = string.Join(';', parts.Select(p =>
            p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ? $"Data Source={path}" : p));
        if (!hasForeignKeys) result += ";Foreign Keys=True";
        return result;
    }

    /// <summary>Walks up from the app base directory to find the repository root.
    /// Marker priority: .git (true repo root), then pyproject.toml, then RecipeApp.sln.</summary>
    private static string? FindRepoRoot()
    {
        foreach (var marker in new[] { ".git", "pyproject.toml", "RecipeApp.sln" })
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, marker)) || Directory.Exists(Path.Combine(dir.FullName, marker)))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        return null;
    }
}
