// Local-time-safe date helpers for the meal plan (YYYY-MM-DD strings, no UTC parsing).

export function parseISO(s) {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function toISO(date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

/** Monday (Date) of the week containing the given YYYY-MM-DD string. */
export function mondayOf(isoStr) {
  const d = parseISO(isoStr);
  const offset = (d.getDay() + 6) % 7; // JS getDay: Sun=0..Sat=6 -> Mon=0..Sun=6
  d.setDate(d.getDate() - offset);
  return d;
}

export function addDays(date, n) {
  const d = new Date(date);
  d.setDate(d.getDate() + n);
  return d;
}

export function todayISO() {
  return toISO(new Date());
}

/** "Oct 19, 2026" */
export function fmtLong(date) {
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}
