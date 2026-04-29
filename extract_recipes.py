import re
import os
import json
import sys
import requests

# Configuration
INPUT_FILE = "Recipes.md"
OUTPUT_DIR = "recipes_json"
OLLAMA_URL = "http://localhost:11434/api/chat"
MODEL_NAME = "gemma4:26b"

# The prompt for the LLM
PROMPT_TEMPLATE = """
Extract the recipe details from the following text.
The output must be a valid JSON object with exactly these keys:
- "title": The name of the recipe.
- "ingredients": An array of strings, one for each ingredient.
- "instructions": An array of strings, one for each step of the process.

If a piece of information is missing, use an empty array or null.

Text:
{text}

JSON Output:
"""

def split_recipes(file_path):
    """Splits the markdown file into chunks based on '##' headers."""
    if not os.path.exists(file_path):
        print(f"Error: {file_path} not found.")
        return []

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Split by '##' but keep the header in the chunk
    # We use a lookahead to split before the '##'
    chunks = re.split(r'\n(?=## )', content)

    # Filter out chunks that don't actually contain a recipe (e.g. the intro)
    recipe_chunks = [c.strip() for c in chunks if c.strip().startswith('##')]
    return recipe_chunks

def slugify(text):
    """Creates a filesystem-friendly name from a title."""
    text = text.lower()
    text = re.sub(r'[^a-z0-9\s-]', '', text)
    return re.sub(r'[\s-]+', '_', text).strip('_')

def extract_recipe_with_llm(chunk):
    """Sends a single chunk to Ollama for extraction."""
    try:
        payload = {
            "model": MODEL_NAME,
            "messages": [
                {"role": "user", "content": PROMPT_TEMPLATE.format(text=chunk)}
            ],
            "stream": False
        }

        response = requests.post(OLLAMA_URL, json=payload)
        response.raise_for_status()

        response_data = response.json()
        # Ollama's /api/chat returns the response in ['message']['content']
        response_text = response_data['message']['content']

        # Find the JSON part (in case the LLM added markdown formatting)
        json_match = re.search(r'(\{.*?\})', response_text, re.DOTALL)
        if json_match:
            return json.loads(json_match.group(1))
        else:
            print("Could not find JSON in LLM response.")
            return None
    except Exception as e:
        print(f"Error during LLM extraction: {e}")
        return None

def main():
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)

    if not os.path.exists(INPUT_FILE):
        print(f"Error: {INPUT_FILE} not found.")
        sys.exit(1)

    print(f"Reading {INPUT_FILE}...")
    chunks = split_recipes(INPUT_FILE)
    print(f"Found {len(chunks)} recipes to process.")

    for i, chunk in enumerate(chunks):
        # Extract title from the first line of the chunk (the ## header)
        lines = chunk.split('\n')
        header_line = lines[0]
        title_match = re.search(r'##\s*(.*)', header_line)

        if not title_match:
            print(f"Skipping chunk {i}: No valid title found.")
            continue

        title = title_match.group(1).strip()
        filename = f"{slugify(title)}.json"
        file_path = os.path.join(OUTPUT_DIR, filename)

        print(f"[{i+1}/{len(chunks)}] Processing: {title}...")

        recipe_data = extract_recipe_with_llm(chunk)

        if recipe_data:  # ← fixed
            recipe_data['title'] = title
            with open(file_path, 'w', encoding='utf-8') as f:
                json.dump(recipe_data, f, indent=2, ensure_ascii=False)
            print(f"  -> Saved to {file_path}")
        else:
            print(f"  -> Failed to extract {title}")

    print("\nExtraction complete!")

if __name__ == "__main__":
    main()
