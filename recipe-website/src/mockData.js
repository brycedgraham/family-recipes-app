export const mockCategories = [
  { id: "cat1", name: "Breakfast", description: "Start your day right!" },
  { id: "cat2", name: "Lunch", description: "Midday meals." },
  { id: "cat3", name: "Dinner", description: "Evening feasts." },
  { id: "cat4", name: "Desserts", description: "Sweet treats." },
  { id: "cat5", name: "Quick & Easy", description: "For when you're short on time." },
];

export const mockRecipes = [
  {
    id: "1",
    name: "Classic Pancakes",
    description: "Fluffy and delicious pancakes, a breakfast favorite.",
    categories: ["Breakfast", "Quick & Easy"],
    ingredients: [
      { name: "All-purpose flour", quantity: "1 1/2 cups" },
      { name: "Baking powder", quantity: "3 1/2 tsp" },
      { name: "Salt", quantity: "1 tsp" },
      { name: "White sugar", quantity: "1 tbsp" },
      { name: "Milk", quantity: "1 1/4 cups" },
      { name: "Egg", quantity: "1" },
      { name: "Melted butter", quantity: "3 tbsp" },
    ],
    instructions: [
      "In a large bowl, sift together the flour, baking powder, salt and sugar.",
      "Make a well in the center and pour in the milk, egg and melted butter; mix until smooth.",
      "Heat a lightly oiled griddle or frying pan over medium high heat.",
      "Pour or scoop the batter onto the griddle, using approximately 1/4 cup for each pancake.",
      "Brown on both sides and serve hot."
    ],
    imageUrls: ["https://via.placeholder.com/300x150.png?text=Pancakes"],
    prepTime: 10,
    cookTime: 15,
    servings: 4,
  },
  {
    id: "2",
    name: "Chicken Salad Sandwich",
    description: "A creamy and satisfying chicken salad sandwich.",
    categories: ["Lunch"],
    ingredients: [
      { name: "Cooked chicken, shredded", quantity: "2 cups" },
      { name: "Mayonnaise", quantity: "1/2 cup" },
      { name: "Celery, finely chopped", quantity: "1/4 cup" },
      { name: "Red onion, finely chopped", quantity: "2 tbsp" },
      { name: "Salt", quantity: "to taste" },
      { name: "Black pepper", quantity: "to taste" },
      { name: "Lettuce leaves", quantity: "4" },
      { name: "Bread slices", quantity: "8" },
    ],
    instructions: [
        "In a medium bowl, combine shredded chicken, mayonnaise, celery, and red onion.",
        "Season with salt and pepper to taste. Mix well.",
        "Spread the chicken salad mixture evenly on 4 slices of bread.",
        "Top with lettuce leaves and the remaining 4 slices of bread.",
        "Serve immediately or chill for later."
    ],
    imageUrls: ["https://via.placeholder.com/300x150.png?text=Chicken+Salad"],
    prepTime: 15,
    servings: 4,
  },
  {
    id: "3",
    name: "Spaghetti Carbonara",
    description: "A classic Italian pasta dish.",
    categories: ["Dinner"],
    ingredients: [
        { name: "Spaghetti", quantity: "400g" },
        { name: "Guanciale or Pancetta, diced", quantity: "150g" },
        { name: "Large eggs", quantity: "3" },
        { name: "Pecorino Romano cheese, grated", quantity: "50g" },
        { name: "Black pepper, freshly ground", quantity: "to taste" },
        { name: "Salt", quantity: "to taste" },
    ],
    instructions: [
        "Cook spaghetti according to package directions. Drain, reserving about 1 cup of pasta water.",
        "While pasta cooks, fry guanciale in a large skillet over medium heat until crisp. Remove from heat.",
        "In a bowl, whisk eggs and Pecorino Romano cheese. Season with black pepper.",
        "Add drained pasta to the skillet with guanciale. Toss to combine. If needed, add a little reserved pasta water to create a sauce.",
        "Quickly pour in egg and cheese mixture, stirring constantly to prevent eggs from scrambling. The heat of the pasta will cook the eggs and create a creamy sauce.",
        "Serve immediately, garnished with more Pecorino Romano and black pepper."
    ],
    imageUrls: ["https://via.placeholder.com/300x150.png?text=Carbonara"],
    prepTime: 10, cookTime: 15, servings: 4,
  },
  {
    id: "4",
    name: "Chocolate Lava Cakes",
    description: "Decadent molten chocolate cakes.",
    categories: ["Desserts", "Quick & Easy"],
    ingredients: [
        { name: "Bittersweet chocolate, chopped", quantity: "4 oz" },
        { name: "Unsalted butter", quantity: "1/2 cup (1 stick)" },
        { name: "Large eggs", quantity: "2" },
        { name: "Large egg yolks", quantity: "2" },
        { name: "Granulated sugar", quantity: "1/4 cup" },
        { name: "All-purpose flour", quantity: "2 tbsp" },
    ],
    instructions: [
        "Preheat oven to 425°F (220°C). Grease and flour 4 (6-ounce) ramekins.",
        "Melt chocolate and butter. Whisk eggs, yolks, and sugar. Combine mixtures. Fold in flour.",
        "Divide batter into ramekins. Bake 12-14 mins until edges set, center soft.",
        "Cool briefly, invert onto plates. Serve immediately."
    ],
    imageUrls: ["https://via.placeholder.com/300x150.png?text=Lava+Cake"],
    prepTime: 15, cookTime: 12, servings: 4,
  }
];
