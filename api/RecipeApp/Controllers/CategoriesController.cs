using Microsoft.AspNetCore.Mvc;
using RecipeApp.Core.Interfaces;
using RecipeApp.Dtos;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IRecipeRepository _recipes;

    public CategoriesController(IRecipeRepository recipes)
    {
        _recipes = recipes;
    }

    [HttpGet]
    public async Task<List<CategoryDto>> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _recipes.GetCategoriesAsync(cancellationToken);
        return categories.Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.RecipeCount)).ToList();
    }
}
