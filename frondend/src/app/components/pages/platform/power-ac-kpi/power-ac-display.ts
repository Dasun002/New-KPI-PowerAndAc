/* File: power-ac-display.ts
   Display and input rules for the Power & A/C KPI page, kept free of Angular so they can be unit tested.
   A missing value is never shown or stored as zero. */

// Achievement shown as a percentage. Null or missing shows "—", never 0%.
export function kpiScoreText(value: number | null | undefined): string {
  return value === null || value === undefined || Number.isNaN(value) ? '—' : `${Number(value).toFixed(2)}%`;
}

// Critical Safety target: a plain number of incidents, no % sign (for example "0").
export function safetyTargetText(target: number | null | undefined): string {
  return target === null || target === undefined || Number.isNaN(target) ? '—' : `${target}`;
}

export const SAFETY_MAX_INCIDENTS = 1000;

export type IncidentParse = { ok: true; value: number | null } | { ok: false; message: string };

// Safety Incident Count input. Accepts whole numbers 0 to 1000. An empty box means "not entered" (null), never zero.
// Rejects negatives, decimals, exponents, and text.
export function parseIncidentCount(raw: string): IncidentParse {
  const text = raw.trim();
  if (text === '') return { ok: true, value: null };
  if (!/^\d+$/.test(text)) return { ok: false, message: 'Safety Incident Count must be a whole number, 0 or more.' };
  const value = Number(text);
  if (value > SAFETY_MAX_INCIDENTS) return { ok: false, message: `Safety Incident Count must be ${SAFETY_MAX_INCIDENTS} or less.` };
  return { ok: true, value };
}
