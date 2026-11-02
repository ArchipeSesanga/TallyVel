import 'server-only'
import type { HistoryFilter } from './historyParams'
import { southAfricanDaysToUtcRange } from './saRange'

/** What went wrong with the dates, if anything. The page says so; nothing is shown as an error. */
export type DateProblem = 'invalid-date' | 'to-before-from' | null

/**
 * The filter in the URL → the query the API takes. This is where the South African days from the address bar
 * become the half-open UTC range, through saRange.ts (the only converter). If the dates make no sense, the date
 * filter is dropped (everything is shown) and `dateProblem` says why.
 *
 * Used by the history page AND the /bff/ Route Handler, so page one and every later page are asked the same way.
 */
export function apiQueryFor(filter: HistoryFilter) {
  const range = southAfricanDaysToUtcRange(filter.from ?? undefined, filter.to ?? undefined)
  const dateProblem: DateProblem = range.ok ? null : range.reason

  return {
    dateProblem,
    query: {
      sort: filter.sort,
      ...(filter.member !== null ? { memberUserId: filter.member } : {}),
      ...(range.ok && range.from !== null ? { from: range.from } : {}),
      ...(range.ok && range.to !== null ? { to: range.to } : {}),
    },
  }
}
