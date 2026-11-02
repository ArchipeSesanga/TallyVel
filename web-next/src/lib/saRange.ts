// The ONE place a South African calendar day becomes a UTC instant for filtering.
// Nothing else in the app converts dates for a filter. (nuqs's parseAsIsoDate must not be used:
// it turns "2026-10-31" into midnight UTC, which is 02:00 in South Africa and would lose two hours.)
//
// Africa/Johannesburg is UTC+2 ALL YEAR. South Africa has had no daylight saving since 1944, so a
// South African midnight is always 22:00 UTC the evening before. We rely on that fixed offset and
// use only UTC arithmetic (Date.UTC, getUTC*), never the machine's own time zone, so the results are
// the same on a laptop in London and on a server set to UTC. `npm run test:tz` runs the tests in both.

const SAST_OFFSET_HOURS = 2
const HOUR_MS = 60 * 60 * 1000

/** "2026-10-31" and nothing else. Two-digit month and day, four-digit year. */
const DAY = /^(\d{4})-(\d{2})-(\d{2})$/

/** Midnight (00:00 South African time) at the start of this calendar day, in ms since 1970 UTC. */
function startOfDayMs(year: number, month: number, day: number): number {
  return Date.UTC(year, month - 1, day) - SAST_OFFSET_HOURS * HOUR_MS
}

/**
 * Reads "YYYY-MM-DD" into its parts, or null if it is not a REAL calendar day. "2026-02-30" is refused:
 * Date.UTC would quietly roll it over to 2 March, so we build the date and check that it came back unchanged.
 */
function parseDay(text: string): { year: number; month: number; day: number } | null {
  const match = DAY.exec(text)
  if (match === null) return null
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])]
  const check = new Date(Date.UTC(year, month - 1, day))
  const real = check.getUTCFullYear() === year && check.getUTCMonth() === month - 1 && check.getUTCDate() === day
  return real ? { year, month, day } : null
}

/** True when `text` is a real South African calendar day written as YYYY-MM-DD. The URL parsers use this. */
export function isRealDay(text: string): boolean {
  return parseDay(text) !== null
}

/** "2026-09-30T22:00:00Z": an instant the API accepts (UTC, ends in Z, no milliseconds). */
function toApiInstant(ms: number): string {
  return new Date(ms).toISOString().replace('.000Z', 'Z')
}

export type SaRange =
  | { ok: true; from: string | null; to: string | null }
  | { ok: false; reason: 'invalid-date' | 'to-before-from' }

/**
 * Turns a South African date range, as picked on a calendar, into the half-open UTC range the API takes:
 * `from` INCLUDED, `to` EXCLUDED.
 *
 *   from = 00:00 South African time on the `from` day
 *   to   = 00:00 South African time on the day AFTER the `to` day
 *
 * So "1 to 31 October" is 2026-09-30T22:00:00Z up to (not including) 2026-10-31T22:00:00Z, and a
 * contribution at 23:30 on 31 October (21:30Z) is inside, with nothing from 1 November counted.
 *
 * The rules:
 *   - only `from`: everything from the start of that day onward (no upper end).
 *   - only `to`: everything up to the end of that day (no lower end).
 *   - neither: no filter at all (both null).
 *   - the same day twice: that one day, exactly 24 hours.
 *   - `to` before `from`: { ok: false, reason: 'to-before-from' }. The caller shows everything and says why;
 *     we do not swap the two days, because that would answer a question the person did not ask.
 *   - a date that is not a real calendar day (2026-02-30, "yesterday"): { ok: false, reason: 'invalid-date' }.
 */
export function southAfricanDaysToUtcRange(from?: string, to?: string): SaRange {
  const start = from === undefined || from === '' ? null : parseDay(from)
  const end = to === undefined || to === '' ? null : parseDay(to)
  if ((from !== undefined && from !== '' && start === null) || (to !== undefined && to !== '' && end === null)) {
    return { ok: false, reason: 'invalid-date' }
  }

  const fromMs = start === null ? null : startOfDayMs(start.year, start.month, start.day)
  // Date.UTC(year, month, day + 1) rolls over month and year ends correctly (31 Oct + 1 = 1 Nov).
  const toMs = end === null ? null : startOfDayMs(end.year, end.month, end.day + 1)

  if (fromMs !== null && toMs !== null && toMs - 24 * HOUR_MS < fromMs) {
    return { ok: false, reason: 'to-before-from' }
  }

  return {
    ok: true,
    from: fromMs === null ? null : toApiInstant(fromMs),
    to: toMs === null ? null : toApiInstant(toMs),
  }
}
