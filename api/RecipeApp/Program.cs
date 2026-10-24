using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RecipeApp.Core.Interfaces;
using RecipeApp.Infrastructure.Data;
using RecipeApp.Infrastructure.Repositories;
using RecipeApp.Api;
using RecipeApp.Api.Services;

// `dotnet run -- seed` imports recipes_json/*.json and exits.
if (args.Contains("seed"))
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .Build();
    await SeedData.RunAsync(configuration);
    return;
}
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Database
var connectionString = SqliteDatabase.Resolve(
    builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection"));

builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseSqlite(connectionString));

// Repositories
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddScoped<IShoppingListRepository, ShoppingListRepository>();
builder.Services.AddScoped<IMealPlanRepository, MealPlanRepository>();

// Recipe images: physical files live next to the database (data/images) and are served at /api/images,
// so image requests go through the same /api origin as everything else (dev proxy + single-process prod).
var imagesDirectory = Path.Combine(SqliteDatabase.DataDirectory ?? throw new InvalidOperationException("Database not resolved."), "images");
builder.Services.AddSingleton(new ImageStore(imagesDirectory));

var app = builder.Build();

// Create the SQLite database on first run if it does not exist yet (same behavior as the seed command).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
    await db.Database.MigrateAsync();
    // Backfill store / pantry-staple defaults for ingredients seeded before that support existed.
    await RecipeApp.Infrastructure.Data.IngredientBackfill.ApplyDefaultsAsync(db);
}
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Single-process hosting: when the built SvelteKit SPA (client/dist) exists, serve it from this server.
var repoRoot = Path.GetFullPath(Path.Combine(SqliteDatabase.DataDirectory!, ".."));
var spaDirectory = Path.Combine(repoRoot, "client", "dist");
if (!Directory.Exists(spaDirectory)) spaDirectory = null;

if (spaDirectory is not null)
{
    var spaProvider = new PhysicalFileProvider(spaDirectory);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = spaProvider, DefaultFileNames = ["index.html"] });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = spaProvider });
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesDirectory),
    RequestPath = "/api/images",
});

// Pin routing to run AFTER the static-file middleware above. Without an explicit UseRouting,
// .NET 10 lets endpoint matching (incl. the /api/{**rest} catch-all below) beat UseStaticFiles,
// which would 404 every /api/images/... request instead of serving the file.
app.UseRouting();

// HTTPS redirection is only useful for local dev (launchSettings https profile); on the LAN box it would 307 to a dead port.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

if (spaDirectory is not null)
{
    // Unmatched non-API routes fall back to the SPA shell; unmatched /api/* routes must 404 instead.
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = new PhysicalFileProvider(spaDirectory) });
}
app.Map("/api/{**rest}", () => Results.NotFound(new { error = "Not found." }));

app.Run();
