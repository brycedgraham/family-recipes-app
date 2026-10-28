using Microsoft.AspNetCore.Mvc;
using RecipeApp.Core;
using RecipeApp.Core.Interfaces;
using RecipeApp.Core.Models;
using RecipeApp.Dtos;
using RecipeApp.Api.Services;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeRepository _recipes;
    private readonly ImageStore _images;
    private readonly RecipeUrlImporter _importer;

    public RecipesController(IRecipeRepository recipes, ImageStore images, RecipeUrlImporter importer)
    {
        _recipes = recipes;
        _images = images;
        _importer = importer;
    }

    [HttpGet]
    public async Task<List<RecipeDto>> GetAll([FromQuery] string? q, [FromQuery] string? category, CancellationToken cancellationToken)
    {
        var recipes = await _recipes.GetAllAsync(q, category, cancellationToken);
        return recipes.Select(RecipeMapper.ToDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var recipe = await _recipes.GetByIdAsync(id);
        return recipe is null ? NotFound() : Ok(RecipeMapper.ToDto(recipe));
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create([FromBody] RecipeRequest request)
    {
        var recipe = new Recipe();
        await ApplyAsync(recipe, request);
        var created = await _recipes.AddAsync(recipe);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, RecipeMapper.ToDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Update(Guid id, [FromBody] RecipeRequest request)
    {
        var recipe = await _recipes.GetByIdAsync(id);
        if (recipe is null) return NotFound();

        recipe.RecipeIngredients.Clear();
        recipe.Steps.Clear();
        recipe.Categories.Clear();
        await ApplyAsync(recipe, request);
        var updated = await _recipes.UpdateAsync(recipe);
        return Ok(RecipeMapper.ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _recipes.DeleteAsync(id);
        if (deleted) _images.DeleteRecipeDirectory(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Uploads one or more photos for the recipe (multipart field "files").</summary>
    [HttpPost("{id:guid}/images")]
    public async Task<ActionResult<RecipeDto>> AddImages(Guid id, [FromForm] IFormFileCollection files)
    {
        if (files.Count == 0) return BadRequest("No files provided.");
        var recipe = await _recipes.GetByIdAsync(id);
        if (recipe is null) return NotFound();

        foreach (var file in files)
        {
            if (!ImageStore.IsAllowedFile(file))
                return BadRequest($"Unsupported image: use .jpg, .jpeg, .png, .webp or .gif, max 10 MB per file.");
        }

        var now = DateTime.UtcNow;
        foreach (var file in files)
        {
            recipe.Images.Add(new RecipeImage
            {
                RecipeId = id,
                FileName = _images.Save(id, file),
                SortOrder = recipe.Images.Count + 1,
                CreatedAt = now,
            });
        }

        var updated = await _recipes.UpdateAsync(recipe);
        return Ok(RecipeMapper.ToDto(updated));
    }

    /// <summary>Imports a recipe from the schema.org/Recipe JSON-LD embedded in an arbitrary web page.</summary>
    [HttpPost("import-url")]
    public async Task<ActionResult<RecipeDto>> ImportFromUrl([FromBody] RecipeImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Url))
            return BadRequest("Provide the recipe page URL as { \"url\": \"https://...\" }.");

        ImportedRecipe imported;
        try
        {
            imported = await _importer.ImportAsync(request.Url.Trim(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the request itself was aborted
        }
        catch (RecipeImportException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex) // network failures, DNS errors, timeouts…
        {
            return BadRequest($"Could not fetch that URL: {ex.Message}");
        }

        var recipe = new Recipe
        {
            Title = imported.Title,
            Description = imported.Description,
            Servings = imported.Servings,
            PrepTimeMinutes = imported.PrepTimeMinutes,
            CookTimeMinutes = imported.CookTimeMinutes,
            Cuisine = imported.Cuisine,
        };

        foreach (var categoryName in imported.Categories)
        {
            var category = await _recipes.FindOrCreateCategoryAsync(categoryName);
            if (!recipe.Categories.Any(c => c.Id == category.Id))
                recipe.Categories.Add(category);
        }

        foreach (var line in imported.Ingredients)
        {
            // FindOrCreateIngredientAsync applies the aisle classifier, store hints and pantry-staple defaults,
            // so imported ingredients inherit the same normalization as seeded ones.
            var ingredient = await _recipes.FindOrCreateIngredientAsync(line.Name, line.Notes);
            if (recipe.RecipeIngredients.Any(ri => ri.IngredientId == ingredient.Id)) continue; // duplicate line
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Quantity = line.Quantity,
                Unit = line.Unit,
                IngredientId = ingredient.Id,
            });
        }

        var stepNumber = 1;
        foreach (var step in imported.Steps)
            recipe.Steps.Add(new RecipeStep { StepNumber = stepNumber++, Text = step });

        var created = await _recipes.AddAsync(recipe);

        // Best-effort hero photo: a failed download must not fail the import.
        if (imported.ImageUrl is not null)
        {
            try
            {
                var bytes = await _importer.DownloadImageAsync(imported.ImageUrl, cancellationToken);
                recipe.Images.Add(new RecipeImage
                {
                    RecipeId = created.Id,
                    FileName = _images.SaveBytes(created.Id, bytes),
                    SortOrder = 1,
                    CreatedAt = DateTime.UtcNow,
                });
                await _recipes.UpdateAsync(recipe);
            }
            catch (Exception)
            {
                // Keep the recipe even when the photo could not be downloaded or validated.
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, RecipeMapper.ToDto(created));
    }

    /// <summary>Removes one photo (DB row + file on disk).</summary>
    [HttpDelete("{id:guid}/images/{fileName}")]
    public async Task<ActionResult<RecipeDto>> DeleteImage(Guid id, string fileName)
    {
        var recipe = await _recipes.GetByIdAsync(id);
        if (recipe is null) return NotFound();

        var image = recipe.Images.FirstOrDefault(i => i.FileName == fileName);
        if (image is null) return NotFound();

        recipe.Images.Remove(image);
        var updated = await _recipes.UpdateAsync(recipe);
        _images.DeleteFile(id, fileName);
        return Ok(RecipeMapper.ToDto(updated));
    }

    /// <summary>Makes the given photo the recipe's cover (first in the gallery). All rows are renumbered so
    /// SortOrder stays collision-free even after earlier deletions.</summary>
    [HttpPut("{id:guid}/images/{fileName}/cover")]
    public async Task<ActionResult<RecipeDto>> SetCoverImage(Guid id, string fileName)
    {
        var recipe = await _recipes.GetByIdAsync(id);
        if (recipe is null) return NotFound();

        var images = recipe.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.CreatedAt).ToList();
        var target = images.FirstOrDefault(i => i.FileName == fileName);
        if (target is null) return NotFound();

        var rest = images.Where(i => !ReferenceEquals(i, target)).ToList();
        target.SortOrder = 0;
        for (var idx = 0; idx < rest.Count; idx++)
        {
            rest[idx].SortOrder = idx + 1;
        }

        var updated = await _recipes.UpdateAsync(recipe);
        return Ok(RecipeMapper.ToDto(updated));
    }

    private async Task ApplyAsync(Recipe recipe, RecipeRequest request)
    {
        recipe.Title = request.Name.Trim();
        recipe.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        recipe.Servings = request.Servings;
        recipe.PrepTimeMinutes = request.PrepTime;
        recipe.CookTimeMinutes = request.CookTime;

        foreach (var categoryName in request.Categories ?? [])
        {
            if (string.IsNullOrWhiteSpace(categoryName)) continue;
            var category = await _recipes.FindOrCreateCategoryAsync(categoryName);
            if (!recipe.Categories.Any(c => c.Id == category.Id))
            {
                recipe.Categories.Add(category);
            }
        }

        foreach (var input in request.Ingredients ?? [])
        {
            if (string.IsNullOrWhiteSpace(input.Name)) continue;
            var (quantity, unit) = IngredientText.ParseQuantity(input.Quantity);
            var ingredient = await _recipes.FindOrCreateIngredientAsync(input.Name);
            if (recipe.RecipeIngredients.Any(ri => ri.IngredientId == ingredient.Id)) continue; // duplicate line
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Quantity = quantity,
                Unit = unit,
                IngredientId = ingredient.Id,
            });
        }

        var stepNumber = 1;
        foreach (var step in request.Instructions ?? [])
        {
            if (string.IsNullOrWhiteSpace(step)) continue;
            recipe.Steps.Add(new RecipeStep { StepNumber = stepNumber++, Text = step.Trim() });
        }
    }
}
