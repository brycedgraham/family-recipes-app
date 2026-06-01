/**
 * @typedef {object} Ingredient
 * @property {string} name - The name of the ingredient.
 * @property {string} quantity - The amount of the ingredient (e.g., "1 cup", "200g").
 */

/**
 * @typedef {object} Recipe
 * @property {string} id - A unique identifier for the recipe.
 * @property {string} name - The name of the recipe.
 * @property {string} description - A short description of the recipe.
 * @property {string[]} categories - An array of category names the recipe belongs to.
 * @property {Ingredient[]} ingredients - An array of ingredients for the recipe.
 * @property {string[]} instructions - An array of steps for preparing the recipe.
 * @property {string[]} imageUrls - An array of URLs for images of the recipe.
 * @property {number} [prepTime] - Preparation time in minutes (optional).
 * @property {number} [cookTime] - Cooking time in minutes (optional).
 * @property {number} [servings] - Number of servings (optional).
 */

/**
 * @typedef {object} Category
 * @property {string} id - A unique identifier for the category.
 * @property {string} name - The name of the category (e.g., "Desserts", "Main Courses").
 * @property {string} [description] - An optional description for the category.
 */

// Example Usage (for illustration, will be replaced by actual data handling)
const exampleRecipe = {
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
  imageUrls: ["/images/pancakes.jpg"], // Placeholder path
  prepTime: 10,
  cookTime: 15,
  servings: 4,
};

const exampleCategory = {
  id: "cat1",
  name: "Breakfast",
  description: "Start your day right with these delicious breakfast recipes."
};

export {}; // To make this a module
