import argparse
import json
import os
import re

def split_by_categories(content):
    """
    Splits the markdown content by level 1 headers.
    Returns a list of dictionaries, each containing 'name' and 'content'.
    """
    # Regex to find level 1 headers: # HeaderName
    # We use re.MULTILINE to allow ^ to match start of lines.
    # The header might look like # **Name**  {#slug}
    pattern = re.compile(r'^#\s+\*\*?(.*?)\*\*?.*$', re.MULTILINE)
    
    matches = list(pattern.finditer(content))
    
    categories = []
    for i in range(len(matches)):
        header_name = matches[i].group(1).strip()
        start_pos = matches[i].end()
        end_pos = matches[i+1].start() if i + 1 < len(matches) else len(content)
        category_content = content[start_pos:end_pos].strip()
        
        categories.append({
            "name": header_name,
            "content": category_content
        })
        
    return categories

def split_into_recipes(category_content):
    """
    Splits the category content by level 2 headers.
    Returns a list of dictionaries, each containing 'name' and 'content'.
    """
    # Regex to find level 2 headers: ## RecipeName {#slug} or ## RecipeName
    # The header might look like ## RecipeName {#slug}
    pattern = re.compile(r'^##\s+(.*?)(?:\s+\{#.*?\})?$', re.MULTILINE)
    
    matches = list(pattern.finditer(category_content))
    
    recipes = []
    for i in range(len(matches)):
        recipe_name = matches[i].group(1).strip()
        start_pos = matches[i].end()
        end_pos = matches[i+1].start() if i + 1 < len(matches) else len(category_content)
        recipe_content = category_content[start_pos:end_pos].strip()
        
        recipes.append({
            "name": recipe_name,
            "content": recipe_content
        })
        
    return recipes

def main():
    parser = argparse.ArgumentParser(description="Parse recipes from a file.")
    parser.add_argument("input_file", help="Path to the input file (e.g., Recipes.md)")
    parser.add_argument("--output", help="Path to the output JSON file", default="recipes.json")
    
    args = parser.parse_args()

    if not os.path.exists(args.input_file):
        print(f"Error: Input file '{args.input_file}' not found.")
        return

    print(f"Parsing {args.input_file}...")
    
    with open(args.input_file, "r", encoding="utf-8") as f:
        content = f.read()

    categories = split_by_categories(content)
    
    print(f"Found {len(categories)} categories: {', '.join([c['name'] for c in categories])}")

    parsed_data = []
    for cat in categories:
        print(f"  Parsing category: {cat['name']}")
        recipes = split_into_recipes(cat['content'])
        print(f"    Found {len(recipes)} recipes")
        
        parsed_data.append({
            "category": cat['name'],
            "recipes": recipes
        })

    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(parsed_data, f, indent=4)
    
    print(f"Successfully parsed recipes to {args.output}")

if __name__ == "__main__":
    main()
