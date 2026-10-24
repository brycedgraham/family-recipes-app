using System.Text.RegularExpressions;

namespace RecipeApp.Core;

/// <summary>
/// Lightweight grocery-aisle classifier for normalized ingredient names.
/// Keyword hints only: categories are checked in order and the first match wins, so specific
/// compound keywords ("chicken broth" -> Canned) must be listed before generic ones ("chicken" -> Meat).
/// Unknown names get "Other".
/// </summary>
public static class IngredientCategoryClassifier
{
    private sealed record Rule(string Category, string[] Keywords);

    private static readonly Rule[] Rules =
    {
        new("Frozen", new[] { "frozen", "ice cream", "popsicle" }),
        new("Canned & Jarred", new[]
        {
            "canned", "jarred", "broth", "stock", "tomato sauce", "tomato paste", "salsa", "relish",
            "diced tomatoes", "crushed tomatoes", "stewed tomatoes",
            "pickle", "pickles", "olives", "black bean", "black beans", "kidney bean", "kidney beans", "condensed soup", "soup",
        }),
        new("Baking", new[]
        {
            "flour", "sugar", "baking powder", "baking soda", "yeast", "chocolate", "cocoa", "vanilla",
            "cornstarch", "gelatin", "pudding", "shortening", "icing",
        }),
        new("Spices & Seasonings", new[]
        {
            "salt", "spice", "seasoning", "paprika", "cumin", "oregano", "basil", "cilantro", "parsley",
            "thyme", "rosemary", "sage", "mint", "saffron", "cinnamon", "nutmeg", "curry", "bay leaf", "bay leaves", "dill", "peppercorns",
            "garlic powder", "onion powder", "chili powder", "pepper flakes", "black pepper", "white pepper", "bouillon",
        }),
        new("Oils & Condiments", new[]
        {
            "oil", "vinegar", "soy sauce", "ketchup", "mustard", "mayonnaise", "honey", "syrup", "jam",
            "jelly", "peanut butter", "hot sauce", "worcestershire", "fish sauce", "hoisin", "barbecue sauce", "sauce", "dressing", "juice",
        }),
        new("Dairy & Eggs", new[]
        {
            "egg", "eggs", "butter", "milk", "cream", "cheese", "yogurt", "parmesan", "mozzarella", "cheddar",
            "feta", "ricotta", "sour cream", "half and half", "half & half",
        }),
        new("Meat & Seafood", new[]
        {
            "chicken", "beef", "pork", "bacon", "ham", "sausage", "turkey", "lamb", "ground", "steak",
            "meatball", "salmon", "shrimp", "prawn", "tuna", "crab", "lobster", "fish", "anchovy",
            "hamburger", "hamburgers", "burger", "hotdog", "hotdogs", "steaks", "meat", "fillet", "fillets",
            "pepperoni", "prosciutto", "hot dog", "giblet",
        }),
        new("Bakery & Bread", new[]
        {
            "bread", "bun", "buns", "roll", "rolls", "baguette", "tortilla", "pita", "naan", "crouton",
            "biscuit", "biscuits", "waffle", "pancake", "croissant", "bagel", "pretzel",
            "muffin", "muffins", "tortillas",
        }),
        new("Produce", new[]
        {
            "onion", "onions", "garlic", "tomato", "tomatoes", "potato", "potatoes", "carrot", "carrots", "celery", "pepper", "lemon", "lime", "orange",
            "apple", "banana", "berry", "berries", "corn", "pea", "peas", "bean", "beans", "broccoli",
            "cauliflower", "zucchini", "cucumber", "mushroom", "mushrooms", "squash", "radish", "turnip",
            "beet", "beets", "sweet potato", "ginger", "scallion", "leek", "leeks", "chive", "herb", "herbs",
            "spinach", "kale", "lettuce", "cabbage", "asparagus", "avocado",
            "scallions", "bananas", "zuccini", "bok choy", "cranberries", "raspberries",
        }),
        new("Dry Goods & Pasta", new[]
        {
            "pasta", "rice", "couscous", "noodle", "noodles", "oat", "oats", "cereal", "quinoa", "bulgur",
            "lentil", "lentils", "chickpea", "chickpeas", "barley", "vermicelli", "orzo", "semolina", "grits",
            "spaghetti", "macaroni", "rotini", "panko",
        }),
    };

    // Precompiled: single words match on word boundaries, phrases (e.g. "black pepper") as substrings.
    private static readonly (string Category, Regex[] Patterns)[] Compiled = Rules
        .Select(r => (r.Category, r.Keywords.Select(k => k.Contains(' ')
            ? new Regex(Regex.Escape(k), RegexOptions.IgnoreCase)
            : new Regex($@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase)).ToArray()))
        .ToArray();

    /// <summary>Maps a normalized ingredient name to a grocery aisle category; "Other" when no keyword matches.</summary>
    public static string GuessCategory(string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedName)) return "Other";
        foreach (var (category, patterns) in Compiled)
            foreach (var pattern in patterns)
                if (pattern.IsMatch(normalizedName)) return category;
        return "Other";
    }
}
