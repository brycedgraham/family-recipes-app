using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Core;
using RecipeApp.Core.Models;
using RecipeApp.Infrastructure.Data;
using RecipeApp.Infrastructure.Repositories;

namespace RecipeApp.Api;

/// <summary>
/// Imports recipes_json/*.json into the database. Idempotent: titles that already exist are skipped,
/// so re-running after adding new JSON files only inserts what's new.
/// </summary>
public static class SeedData
{
    private sealed record RecipeFile(string Title, string? Category, string[] Ingredients, string[] Instructions);

    public static async Task RunAsync(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");

        var jsonDir = FindRecipesJsonDirectory()
            ?? throw new DirectoryNotFoundException("Could not find a recipes_json directory at or above the working directory.");

        var options = new DbContextOptionsBuilder<RecipeDbContext>()
            .UseSqlite(SqliteDatabase.Resolve(connectionString))
            .Options;

        await using var db = new RecipeDbContext(options);
        await db.Database.MigrateAsync();
        // Backfill store / pantry-staple defaults for ingredients seeded before that support existed.
        await IngredientBackfill.ApplyDefaultsAsync(db);

        var repo = new RecipeRepository(db);
        var existingTitles = (await db.Recipes.Select(r => r.Title).ToListAsync()).ToHashSet(StringComparer.Ordinal);

        var files = Directory.GetFiles(jsonDir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var file in files)
        {
            try
            {
                var raw = await File.ReadAllTextAsync(file);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    Console.WriteLine($"skip (empty): {Path.GetFileName(file)}");
                    skipped++;
                    continue;
                }

                var data = JsonSerializer.Deserialize<RecipeFile>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data is null || string.IsNullOrWhiteSpace(data.Title))
                {
                    Console.WriteLine($"skip (no title): {Path.GetFileName(file)}");
                    skipped++;
                    continue;
                }

                var title = data.Title.Trim();
                if (existingTitles.Contains(title))
                {
                    Console.WriteLine($"skip (exists): {title}");
                    skipped++;
                    continue;
                }

                var recipe = new Recipe { Title = title };

                if (!string.IsNullOrWhiteSpace(data.Category))
                {
                    recipe.Categories.Add(await repo.FindOrCreateCategoryAsync(data.Category));
                }

                foreach (var line in data.Ingredients ?? [])
                {
                    var cleaned = IngredientText.CleanLine(line);
                    if (cleaned is null) continue;
                    var (quantity, unit, name, notes) = IngredientText.SplitLine(cleaned);
                    var ingredient = await repo.FindOrCreateIngredientAsync(name.Length > 0 ? name : cleaned, string.IsNullOrWhiteSpace(notes) ? null : notes);
                    recipe.RecipeIngredients.Add(new RecipeIngredient
                    {
                        Quantity = quantity,
                        Unit = unit,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                        IngredientId = ingredient.Id,
                    });
                }

                var stepNumber = 1;
                foreach (var step in data.Instructions ?? [])
                {
                    if (string.IsNullOrWhiteSpace(step)) continue;
                    recipe.Steps.Add(new RecipeStep { StepNumber = stepNumber++, Text = step.Trim() });
                }

                await repo.AddAsync(recipe);
                existingTitles.Add(title);
                added++;
                Console.WriteLine($"added: {title}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"error: {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nSeeding complete: {added} added, {skipped} skipped, {failed} failed ({files.Count()} files in {jsonDir}).");
    }

    private static string? FindRecipesJsonDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = new DirectoryInfo(Path.Combine(dir.FullName, "recipes_json"));
            if (candidate.Exists) return candidate.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
