using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RecipeApp.Core.Interfaces;
using RecipeApp.Infrastructure.Data;
using RecipeApp.Infrastructure.Repositories;
using RecipeApp.Api;

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
builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
