import re
from typing import List, Dict


def parse_recipes(markdown_content: str) -> List[Dict[str, str]]:
    """
    Parse a markdown file into a list of recipe objects.
    
    Each object contains:
      - category: The top-level category (e.g., "Breakfast")
      - title: The recipe title (e.g., "Apple Butter Pancakes")
      - instructions: The recipe content (ingredients + instructions)
    
    Expected markdown structure:
      # **Category**  {#category-id}
      ## Recipe Title {#recipe-id}
      Content...
    """
    recipes = []
    current_category = None
    current_title = None
    current_content = []

    def save_recipe():
        nonlocal current_title, current_content
        if current_title and current_category:
            content = '\n'.join(current_content).strip()
            if content:  # Only save if there's actual content
                recipes.append({
                    'category': current_category,
                    'title': current_title,
                    'instructions': content
                })
        current_title = None
        current_content = []

    lines = markdown_content.split('\n')

    for line in lines:
        # Check for category heading: # **Breakfast**  {#breakfast}
        category_match = re.match(r'^#\s+\*\*(.+?)\*\*', line)
        if category_match:
            save_recipe()
            current_category = category_match.group(1).strip()
            continue

        # Check for recipe heading: ## Apple Butter Pancakes {#apple-butter-pancakes}
        recipe_match = re.match(r'^##\s+(.+?)\s*\{#', line)
        if recipe_match:
            save_recipe()
            current_title = recipe_match.group(1).strip()
            continue

        # If we're inside a recipe, collect content
        if current_title is not None:
            current_content.append(line)

    # Save the last recipe
    save_recipe()

    return recipes


def main():
    with open('Recipes.md', 'r', encoding='utf-8') as f:
        content = f.read()

    recipes = parse_recipes(content)

    for recipe in recipes:
        print(f"Category: {recipe['category']}")
        print(f"Title:    {recipe['title']}")
        preview = recipe['instructions'][:100].encode('ascii', errors='replace').decode('ascii')
        print(f"Content:  {preview}...")
        print("-" * 60)

    print(f"\nTotal recipes parsed: {len(recipes)}")


if __name__ == '__main__':
    main()