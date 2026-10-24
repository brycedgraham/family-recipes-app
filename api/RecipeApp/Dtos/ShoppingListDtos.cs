using RecipeApp.Core.Models;

namespace RecipeApp.Dtos;

// Response shapes mirror the client's data model (client/src/dataModels.js):
// item quantity stays a decimal + unit pair so the client can reformat after scaling.

public record ShoppingListItemDto(
    Guid Id,
    string Name,
    string Category,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    string? RecipeSources,
    bool IsChecked,
    int SortOrder,
    string? Store,
    bool IsPantryStaple);

public record ShoppingListSummaryDto(
    Guid Id,
    string Name,
    int ItemCount,
    int CheckedCount,
    DateTime UpdatedAt,
    string ShareToken);

public record ShoppingListDto(
    Guid Id,
    string Name,
    string ShareToken,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ShoppingListItemDto> Items);

// Requests
public record CreateShoppingListRequest(string? Name, List<RawTextItem>? Items);
public record RawTextItem(string RawText, string? Store = null);
public record AddRecipesRequest(List<RecipeMultiplier> Items);
public record RecipeMultiplier(Guid RecipeId, decimal Multiplier = 1m);
public record PatchItemRequest(bool? IsChecked, string? Name, string? Category, decimal? Quantity, string? Unit, string? Store = null, bool? IsPantryStaple = null);

public static class ShoppingListMapper
{
    // Ticket ordering for full lists: Category (uncategorized last), then IsChecked, then Name.
    public static ShoppingListDto ToDto(ShoppingList list) => new(
        list.Id,
        list.Name,
        list.ShareToken,
        list.CreatedAt,
        list.UpdatedAt,
        list.Items
            .OrderBy(i => string.IsNullOrEmpty(i.Category))
            .ThenBy(i => i.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.IsChecked)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToItemDto)
            .ToList());

    public static ShoppingListSummaryDto ToSummaryDto(ShoppingList list) => new(
        list.Id,
        list.Name,
        list.Items.Count,
        list.Items.Count(i => i.IsChecked),
        list.UpdatedAt,
        list.ShareToken);

    private static ShoppingListItemDto ToItemDto(ShoppingListItem item) => new(
        item.Id,
        item.Name,
        item.Category,
        item.Quantity,
        item.Unit,
        item.Notes,
        item.RecipeSources,
        item.IsChecked,
        item.SortOrder,
        item.Store,
        item.IsPantryStaple);
}
