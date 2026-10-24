using Microsoft.AspNetCore.Mvc;
using RecipeApp.Core.Interfaces;

namespace RecipeApp.Api.Controllers;

[ApiController]
public class StoresController : ControllerBase
{
    private readonly IShoppingListRepository _lists;

    public StoresController(IShoppingListRepository lists)
    {
        _lists = lists;
    }

    /// <summary>Store names shopping lines can be routed to: the defaults first, then any custom stores in use.</summary>
    [HttpGet("api/stores")]
    public async Task<List<string>> GetStores(CancellationToken cancellationToken)
    {
        return (await _lists.GetKnownStoresAsync(cancellationToken)).ToList();
    }
}
