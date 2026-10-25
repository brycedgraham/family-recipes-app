using System.IO.Compression;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using RecipeApp.Infrastructure.Data;

namespace RecipeApp.Api.Controllers;

[ApiController]
public class BackupController : ControllerBase
{
    private readonly IConfiguration _config;

    public BackupController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// One-click off-site backup: streams a .zip containing a consistent standalone SQLite
    /// snapshot (created with VACUUM INTO — no lock, no app stop) plus every file under
    /// data/images/. The family can tap "Download Backup" in the sidebar to grab a copy for
    /// their phone or PC.
    /// </summary>
    [HttpGet("api/backup")]
    public async Task<IActionResult> DownloadBackup(CancellationToken cancellationToken)
    {
        var connectionString = SqliteDatabase.Resolve(
            _config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection"));

        // The resolved connection string carries the absolute DB path; images live next to it.
        var dataSource = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));
        var dbPath = dataSource["Data Source=".Length..].Trim();
        var imagesDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "images");

        var tempDb = Path.Combine(Path.GetTempPath(), $"recipeapp_backup_{Guid.NewGuid():N}.db");
        var tempZip = Path.Combine(Path.GetTempPath(), $"recipeapp_backup_{Guid.NewGuid():N}.zip");
        try
        {
            // VACUUM INTO reads the live database and writes a fresh, self-contained file.
            // It does not modify or lock out the running app.
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"VACUUM INTO '{tempDb.Replace("'", "''")}'";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // Build the archive to a temp file first: ZipArchive flushes synchronously on
            // dispose, which Kestrel's async-only response stream rejects.
            await using (var zipFile = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
            await using (var zip = new ZipArchive(zipFile, ZipArchiveMode.Create))
            {
                var dbEntry = zip.CreateEntry("recipes.db");
                await using (var target = dbEntry.Open())
                await using (var source = System.IO.File.OpenRead(tempDb))
                    await source.CopyToAsync(target, cancellationToken);

                if (Directory.Exists(imagesDir))
                {
                    foreach (var file in Directory.EnumerateFiles(imagesDir, "*", SearchOption.AllDirectories)
                                 .OrderBy(p => p, StringComparer.Ordinal))
                    {
                        var relative = Path.GetRelativePath(imagesDir, file).Replace('\\', '/');
                        var entry = zip.CreateEntry($"images/{relative}");
                        await using var target = entry.Open();
                        await using var source = System.IO.File.OpenRead(file);
                        await source.CopyToAsync(target, cancellationToken);
                    }
                }
            }

            Response.ContentType = "application/zip";
            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"recipeapp-backup-{DateTime.Now:yyyy-MM-dd}.zip"
            }.ToString();

            await using (var archive = System.IO.File.OpenRead(tempZip))
                await archive.CopyToAsync(Response.Body, cancellationToken);

            return new EmptyResult();
        }
        finally
        {
            if (System.IO.File.Exists(tempDb)) System.IO.File.Delete(tempDb);
            if (System.IO.File.Exists(tempZip)) System.IO.File.Delete(tempZip);
        }
    }
}
