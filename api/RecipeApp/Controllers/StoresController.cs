using Microsoft.AspNetCore.Mvc;
using RecipeApp.Core.Interfaces;
using RecipeApp.Dtos;

namespace RecipeApp.Api.Controllers;

[ApiController]
public class StoresController : ControllerBase
{
    private readonly IShoppingListRepository _lists;

    public StoresController(IShoppingListRepository lists)
    {
        _lists = lists;
    }

    /// <summary>Store names shopping lines can be routed to: registered stores first, then any ad-hoc stores in use.</summary>
    [HttpGet("api/stores")]
    public async Task<List<string>> GetStores(CancellationToken cancellationToken)
    {
        return (await _lists.GetKnownStoresAsync(cancellationToken)).ToList();
    }

    /// <summary>Registers a new store. Returns the updated list of known stores.</summary>
    [HttpPost("api/stores")]
    public async Task<IActionResult> CreateStore([FromBody] StoreRequest request, CancellationToken cancellationToken)
    {
        var name = (request?.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > 50) return BadRequest("Store name must be 1-50 characters.");
        return Ok(await _lists.CreateStoreAsync(name, cancellationToken));
    }

    /// <summary>Removes a registered store and unassigns it from items/ingredients. Returns the updated list of known stores.</summary>
    [HttpDelete("api/stores/{name}")]
    public async Task<IActionResult> DeleteStore(string name, CancellationToken cancellationToken)
    {
        return Ok(await _lists.DeleteStoreAsync(name, cancellationToken));
    }
}
