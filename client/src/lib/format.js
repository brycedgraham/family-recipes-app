// Friendly number formatting for ingredient quantities — the client-side equivalent of the API's
// IngredientText.FormatNumber. Decimals within 0.01 of a standard culinary fraction render as an
// ASCII fraction ("0.6666" -> "2/3", "1.3333" -> "1 1/3"); everything else keeps up to four
// decimals with trailing zeros trimmed ("2.5", "4"). Near-integers snap to the integer.

const CULINARY_FRACTIONS = [
  [0.125, '1/8'],
  [0.25, '1/4'],
  [1 / 3, '1/3'],
  [0.375, '3/8'],
  [0.5, '1/2'],
  [0.625, '5/8'],
  [2 / 3, '2/3'],
  [0.75, '3/4'],
  [0.875, '7/8'],
];

/** Trim trailing zeros: 2.50 -> "2.5", 4.00 -> "4". */
function trimZeros(value) {
  return String(Number(value.toFixed(4)));
}

/** Format a number with friendly culinary fractions; null/undefined -> ''. */
export function formatNumber(value) {
  if (value === null || value === undefined) return '';
  const sign = value < 0 ? '-' : '';
  const abs = Math.abs(value);
  const whole = Math.floor(abs);
  const frac = abs - whole;

  // Near-integer values snap to the integer ("0.99" -> "1", "2.004" -> "2").
  if (frac >= 0.99) return sign + String(whole + 1);
  if (frac <= 0.01) return sign + String(whole);

  for (const [target, display] of CULINARY_FRACTIONS) {
    if (Math.abs(frac - target) <= 0.01) {
      return sign + (whole > 0 ? `${whole} ${display}` : display);
    }
  }

  return sign + trimZeros(abs);
}

/** "2" + "gal" -> "2 gal"; missing parts are dropped. */
export function formatQuantity(amount, unit) {
  const q = formatNumber(amount);
  return [q, unit].filter(Boolean).join(' ');
}
