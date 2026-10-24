using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using RecipeApp.Core;

namespace RecipeApp.Api.Services;

/// <summary>Thrown when a page could not be fetched or contains no usable schema.org/Recipe JSON-LD.</summary>
public sealed class RecipeImportException(string message) : Exception(message);

/// <summary>One ingredient line after <see cref="IngredientText.SplitLine"/> normalization, ready for the repository.</summary>
public sealed record ImportedIngredient(decimal? Quantity, string? Unit, string Name, string? Notes);

/// <summary>A recipe extracted from schema.org/Recipe JSON-LD on an arbitrary web page.</summary>
public sealed record ImportedRecipe(
    string Title,
    string? Description,
    int? Servings,
    int? PrepTimeMinutes,
    int? CookTimeMinutes,
    string? Cuisine,
    List<string> Categories,
    List<ImportedIngredient> Ingredients,
    List<string> Steps,
    string? ImageUrl);

/// <summary>
/// Fetches a web page, locates its schema.org/Recipe JSON-LD (top-level object, array item, or @graph entry),
/// and extracts the fields the app stores. Ingredient lines are split with <see cref="IngredientText.SplitLine"/>;
/// aisle category, store preference and pantry-staple defaults are applied later by the repository
/// (<c>FindOrCreateIngredientAsync</c>), so imported recipes inherit the same normalization as seeded ones.
/// </summary>
public sealed partial class RecipeUrlImporter
{
    private const int MaxCategoryLength = 40;
    private const int MaxCategories = 8;
    private const int MaxSteps = 200;

    private readonly HttpClient _http;

    public RecipeUrlImporter(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Fetches the page and extracts the first schema.org/Recipe node found in its JSON-LD blocks.</summary>
    public async Task<ImportedRecipe> ImportAsync(string url, CancellationToken cancellationToken = default)
    {
        var html = await FetchPageAsync(url, cancellationToken);
        var baseUrl = new Uri(url);
        var node = FindRecipeNode(html)
            ?? throw new RecipeImportException("No schema.org/Recipe JSON-LD found at that URL.");
        return Extract(node, baseUrl, html);
    }

    /// <summary>Downloads an image, capping the payload at the 10 MB upload limit.</summary>
    public async Task<byte[]> DownloadImageAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new RecipeImportException($"Image download failed (HTTP {(int)response.StatusCode}).");

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > ImageStore.MaxFileSizeBytes)
                throw new RecipeImportException("Image is larger than 10 MB.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private async Task<string> FetchPageAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new RecipeImportException($"Could not fetch that URL (HTTP {(int)response.StatusCode}).");
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // --- JSON-LD discovery ---------------------------------------------------

    private static readonly Regex ScriptBlockRegex = new(
        @"<script\b[^>]*\btype\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex PageTitleRegex = new(
        @"<title[^>]*>(.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static JsonElement? FindRecipeNode(string html)
    {
        foreach (Match match in ScriptBlockRegex.Matches(html))
        {
            var json = match.Groups[1].Value.Trim();
            if (json.Length == 0) continue;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var node = FindRecipe(doc.RootElement);
                if (node is not null) return node.Value.Clone();
            }
            catch (JsonException)
            {
                // Malformed JSON-LD block: skip it and try the next one.
            }
        }
        return null;
    }

    /// <summary>
    /// Finds a Recipe node among: a top-level object, items of a top-level array, entries of an
    /// @graph array (on either a top-level object or on each array item). @type may be a string or an array.
    /// </summary>
    private static JsonElement? FindRecipe(JsonElement root)
    {
        if (TryGetRecipe(root, out var direct)) return direct;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                if (TryGetRecipe(item, out var node)) return node;
        }

        var graphs = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Object && TryGetGraph(root, out var g))
            graphs.AddRange(g.EnumerateArray());
        else if (root.ValueKind == JsonValueKind.Array)
            foreach (var item in root.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object && TryGetGraph(item, out var ig))
                    graphs.AddRange(ig.EnumerateArray());

        foreach (var entry in graphs)
            if (TryGetRecipe(entry, out var found)) return found;

        return null;
    }

    private static bool TryGetGraph(JsonElement element, out JsonElement graph)
    {
        graph = default;
        return element.TryGetProperty("@graph", out graph) && graph.ValueKind == JsonValueKind.Array;
    }

    private static bool TryGetRecipe(JsonElement element, out JsonElement recipe)
    {
        recipe = default;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("@type", out var type)) return false;

        if (type.ValueKind == JsonValueKind.String)
        {
            if (!IsRecipeType(type.GetString())) return false;
            recipe = element;
            return true;
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in type.EnumerateArray())
                if (t.ValueKind == JsonValueKind.String && IsRecipeType(t.GetString()))
                {
                    recipe = element;
                    return true;
                }
        }

        return false;
    }

    private static bool IsRecipeType(string? value) =>
        string.Equals(value, "Recipe", StringComparison.OrdinalIgnoreCase);

    // --- Field extraction ----------------------------------------------------

    private static ImportedRecipe Extract(JsonElement node, Uri baseUrl, string html)
    {
        var title = GetString(node, "name") ?? PageTitleRegex.Match(html).Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new RecipeImportException("The page's JSON-LD has no recipe name.");

        return new ImportedRecipe(
            WebUtility.HtmlDecode(title).Trim(),
            GetString(node, "description"),
            GetServings(node),
            GetMinutes(node, "prepTime"),
            GetMinutes(node, "cookTime"),
            GetStrings(node, "recipeCuisine").FirstOrDefault(),
            GetCategories(node),
            GetIngredients(node),
            GetSteps(node),
            GetImageUrl(node, baseUrl));
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = WebUtility.HtmlDecode(value.GetString() ?? string.Empty).Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>String or array-of-strings property, HTML-decoded and trimmed. Objects contribute their "name" or "text".</summary>
    private static List<string> GetStrings(JsonElement element, string property)
    {
        var result = new List<string>();
        if (!element.TryGetProperty(property, out var value)) return result;

        void Add(string? raw)
        {
            var text = WebUtility.HtmlDecode(raw ?? string.Empty).Trim();
            if (text.Length > 0) result.Add(text);
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                Add(value.GetString());
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) Add(item.GetString());
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String) Add(name.GetString());
                        else if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) Add(text.GetString());
                    }
                }
                break;
        }
        return result;
    }

    /// <summary>First integer in recipeYield ("4 servings", "6", ["4 people"]).</summary>
    private static int? GetServings(JsonElement element)
    {
        foreach (var value in GetStrings(element, "recipeYield"))
        {
            var match = FirstNumberRegex().Match(value);
            if (match.Success && int.TryParse(match.Value, out var n) && n > 0) return n;
        }
        return null;
    }

    /// <summary>ISO 8601 durations (PT15M, PT1H30M, P1DT2H); falls back to the first bare number ("20 minutes").</summary>
    private static int? GetMinutes(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;

        IEnumerable<string?> values = value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : null)
            : new[] { value.ValueKind == JsonValueKind.String ? value.GetString() : (string?)null };

        foreach (var raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var text = raw.Trim();
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var duration) && duration > TimeSpan.Zero)
                return (int)Math.Round(duration.TotalMinutes);
            var match = FirstNumberRegex().Match(text);
            if (match.Success && int.TryParse(match.Value, out var n)) return n;
        }
        return null;
    }

    /// <summary>recipeCategory plus comma-separated keywords, trimmed, deduped, length- and count-capped.</summary>
    private static List<string> GetCategories(JsonElement element)
    {
        var categories = new List<string>();
        void Add(string value)
        {
            var text = value.Trim();
            if (text.Length == 0 || text.Length > MaxCategoryLength) return;
            if (!categories.Any(c => c.Equals(text, StringComparison.OrdinalIgnoreCase))) categories.Add(text);
        }

        foreach (var value in GetStrings(element, "recipeCategory")) Add(value);
        var keywords = GetString(element, "keywords");
        if (keywords is not null)
            foreach (var part in keywords.Split(',')) Add(part);

        return categories.Take(MaxCategories).ToList();
    }

    /// <summary>Each recipeIngredient line through IngredientText.SplitLine; notes are kept for store-hint detection.</summary>
    private static List<ImportedIngredient> GetIngredients(JsonElement element)
    {
        var result = new List<ImportedIngredient>();
        if (!element.TryGetProperty("recipeIngredient", out var value)) return result;

        IEnumerable<JsonElement> items = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : new[] { value };
        foreach (var item in items)
        {
            string? raw = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object when item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String => t.GetString(),
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var (quantity, unit, name, notes) = IngredientText.SplitLine(raw);
            if (string.IsNullOrWhiteSpace(name)) continue;
            result.Add(new ImportedIngredient(quantity, unit, name.Trim(), notes));
        }
        return result;
    }

    /// <summary>
    /// recipeInstructions as a plain string, an array of strings/HowToStep objects, or a HowToSection tree
    /// (objects with itemListElement) — flattened in document order.
    /// </summary>
    private static List<string> GetSteps(JsonElement element)
    {
        var steps = new List<string>();
        if (!element.TryGetProperty("recipeInstructions", out var value)) return steps;

        void AddStep(string? raw)
        {
            var text = WebUtility.HtmlDecode(raw ?? string.Empty).Trim();
            if (text.Length > 0 && steps.Count < MaxSteps) steps.Add(text);
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                AddStep(value.GetString());
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray()) CollectSteps(item, steps, AddStep, depth: 0);
                break;
            case JsonValueKind.Object:
                CollectSteps(value, steps, AddStep, depth: 0);
                break;
        }
        return steps;
    }

    private static void CollectSteps(JsonElement element, List<string> steps, Action<string?> addStep, int depth)
    {
        if (depth > 5) return; // HowToSection nesting is shallow; guard against pathological loops.

        if (element.ValueKind == JsonValueKind.String)
        {
            addStep(element.GetString());
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;

        if (element.TryGetProperty("itemListElement", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray()) CollectSteps(item, steps, addStep, depth + 1);
            return;
        }

        if (element.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            addStep(text.GetString());
    }

    /// <summary>First usable URL from image: string, array of strings, ImageObject with url, or array of those. Relative URLs resolve against the page.</summary>
    private static string? GetImageUrl(JsonElement element, Uri baseUrl)
    {
        if (!element.TryGetProperty("image", out var value)) return null;

        IEnumerable<JsonElement> items = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : new[] { value };
        foreach (var item in items)
        {
            string? url = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object when item.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String => u.GetString(),
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(url)) continue;

            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var candidate))
            {
                if (candidate.IsAbsoluteUri) return candidate.ToString();
                if (Uri.TryCreate(baseUrl, candidate, out var absolute)) return absolute.ToString();
            }
        }
        return null;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex FirstNumberRegex();
}
