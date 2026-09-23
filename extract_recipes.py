#!/usr/bin/env python3
"""Deterministically extract recipes from Recipes.md into recipes_json/*.json.

No LLM involved — the markdown format is regular enough to parse with rules.

Format handled (see ROADMAP.md for notes):
  # **Category** {#cat-slug}     category header
  ## Title {#recipe-slug}        recipe header
  - ingredient lines: start with a quantity token (digits, 1/2-style or unicode
    fractions like ½ ⅔, "3 1⁄2"), or continue the ingredient block (no terminal
    sentence punctuation, right after another ingredient line); bullets (* / -)
    and trailing two-space line breaks are stripped
  - instruction lines: numbered steps ("1. ...") or prose paragraphs; a full
    sentence before any other content starts the instructions
  - literal `Ingredients` / `Instructions` / `Directions` header lines split the
    sections when present; otherwise classification is positional

Usage:
  python3 extract_recipes.py [--dry-run]
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).parent
MD_PATH = ROOT / "Recipes.md"
OUT_DIR = ROOT / "recipes_json"

FRACTIONS = "½⅓¼¾⅕⅛⅙⅔⅖⅗⅘⅚"
QTY_RE = re.compile(
    rf"^\s*(?:\d+\s+)?(?:{FRACTIONS}|\d+\s*[/⁄]\s*\d+|\d+(?:\.\d+)?)\b"
)
NUMBERED_STEP_RE = re.compile(r"^\s*\d+\.\s+")
BULLET_RE = re.compile(r"^\s*[*•-]\s+")
INGREDIENTS_HDR_RE = re.compile(r"^\s*Ingredients\s*:?\s*$", re.I)
INSTRUCTIONS_HDR_RE = re.compile(r"^\s*(?:Instructions?|Directions)\s*:?\s*$", re.I)
MD_ESCAPE_RE = re.compile(r"\\([!\"#%&'()*+,.\-/:;<=>?@\[\\\]^_`{|}~])")


def clean_line(line: str) -> str:
    """Strip markdown line-break spaces, bullets, and escape backslashes."""
    line = MD_ESCAPE_RE.sub(r"\1", line)
    line = BULLET_RE.sub("", line.rstrip())
    return line.strip()


def is_sentence(line: str) -> bool:
    """Instruction prose, not an ingredient: terminal punctuation, no quantity."""
    return not QTY_RE.match(line) and line.rstrip().endswith((".", "!", "?"))


def classify_no_headers(lines: list[str]) -> tuple[list[str], list[str]]:
    """Positional classification for sections without explicit headers.

    A line joins the ingredient block while it is still going: quantity-starting
    lines always do; short colon-terminated labels before any step (e.g.
    `Bread Dough:`) count as ingredient subheaders; other lines do if they lack
    terminal sentence punctuation and either directly follow another ingredient
    line or sit in the top region (before any instruction prose) under 60 chars."""
    ingredients: list[str] = []
    instructions: list[str] = []
    seen_step = False
    last_was_ing = False

    for raw in lines:
        line = clean_line(raw)
        if not line:
            last_was_ing = False
            continue
        if NUMBERED_STEP_RE.match(line):
            instructions.append(re.sub(r"^\s*\d+\.\s*", "", line))
            seen_step = True
            last_was_ing = False
            continue
        ends_sentence = line.rstrip().endswith((".", "!", "?", ":"))
        label = not seen_step and len(line) < 40 and line.endswith(":")
        if QTY_RE.match(line) or label or (
            not ends_sentence and (last_was_ing or (not seen_step and len(line) < 60))
        ):
            ingredients.append(line)
            last_was_ing = True
        else:
            instructions.append(line)
            seen_step = True
            last_was_ing = False
    return ingredients, instructions


def classify_with_headers(lines: list[str]) -> tuple[list[str], list[str]]:
    """Split on literal Ingredients/Instructions/Directions header lines.

    A full-sentence line before the headers (a description paragraph) is routed
    to instructions rather than ingredients."""
    pre: list[str] = []
    post: list[str] | None = None
    for raw in lines:
        line = clean_line(raw)
        if not line:
            continue
        if INSTRUCTIONS_HDR_RE.match(line):
            post = []
            continue
        if INGREDIENTS_HDR_RE.match(line):
            continue
        (post if post is not None else pre).append(line)
    ingredients = [l for l in pre if not is_sentence(l)]
    instructions = [l for l in pre if is_sentence(l)] + [
        re.sub(r"^\s*\d+\.\s*", "", l) for l in (post or [])
    ]
    return ingredients, instructions


def parse_md(text: str) -> list[dict]:
    parts = re.split(r"^(?=#\s|\#\#\s)", text, flags=re.M)
    recipes = []
    category = None
    for part in parts:
        mcat = re.match(r"^#\s+\*\*(.+?)\*\*\s+\{#[a-z0-9-]+\}", part)
        if mcat:
            category = mcat.group(1).strip()
            continue
        mrec = re.match(r"^##\s+(.+?)\s+\{#([a-z0-9-]+)\}\n?(.*)$", part, re.S)
        if not mrec:
            continue
        title, slug, body = mrec.group(1).strip(), mrec.group(2), mrec.group(3)
        lines = [l for l in body.split("\n")]
        has_headers = any(
            INSTRUCTIONS_HDR_RE.match(clean_line(l)) or INGREDIENTS_HDR_RE.match(clean_line(l))
            for l in lines
        )
        if has_headers:
            ingredients, instructions = classify_with_headers(lines)
        else:
            ingredients, instructions = classify_no_headers(lines)
        recipes.append({
            "title": title,
            "slug": slug,
            "category": category or "Uncategorized",
            "ingredients": ingredients,
            "instructions": instructions,
        })
    return recipes


def main() -> int:
    dry_run = "--dry-run" in sys.argv
    text = MD_PATH.read_text(encoding="utf-8")
    recipes = parse_md(text)

    problems = []
    for r in recipes:
        if not r["ingredients"]:
            problems.append(f"  no ingredients: {r['title']}")
        if not r["instructions"]:
            problems.append(f"  no instructions: {r['title']}")

    print(f"parsed {len(recipes)} recipes from {MD_PATH.name}")
    for r in recipes:
        print(f"\n=== {r['title']}  [{r['category']}]  ({r['slug']})")
        print(f"  ingredients ({len(r['ingredients'])}):")
        for i in r["ingredients"]:
            print(f"    - {i}")
        print(f"  instructions ({len(r['instructions'])}):")
        for i in r["instructions"]:
            shown = i if len(i) <= 100 else i[:97] + "..."
            print(f"    * {shown}")
    if problems:
        print("\nPROBLEMS:")
        print("\n".join(problems))

    if not dry_run:
        OUT_DIR.mkdir(exist_ok=True)
        # Remove stale extraction output so the directory mirrors the markdown.
        for old in OUT_DIR.glob("*.json"):
            old.unlink()
        import json
        for r in recipes:
            payload = {k: r[k] for k in ("title", "category", "ingredients", "instructions")}
            (OUT_DIR / f"{r['slug']}.json").write_text(
                json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
            )
        print(f"\nwrote {len(recipes)} files to {OUT_DIR.relative_to(ROOT)}/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
