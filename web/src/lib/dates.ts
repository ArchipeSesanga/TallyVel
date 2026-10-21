const ZONE = 'Africa/Johannesburg'

const dateTimeFormat = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: ZONE,
})

const dateFormat = new Intl.DateTimeFormat('en-ZA', {
  dateStyle: 'medium',
  timeZone: ZONE,
})

/**
 * An instant from the API ("2026-10-31T22:30:00Z") as South African date and time:
 * "01 Nov 2026, 00:30".
 */
export function formatDateTime(instant: string): string {
  return dateTimeFormat.format(new Date(instant))
}

/**
 * A South African calendar date from the API ("2026-10-31") as "31 Oct 2026".
 * new Date("2026-10-31") is midnight UTC, which is 02:00 the same day in
 * Johannesburg (always UTC+2, no daylight saving), so the day never shifts.
 */
export function formatDate(date: string): string {
  return dateFormat.format(new Date(date))
}