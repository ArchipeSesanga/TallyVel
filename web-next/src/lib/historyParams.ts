// The filters of the contribution history, written ONCE and shared by:
//   - the page (reads them on the server with `loadHistoryParams`),
//   - the Route Handler under /bff/ (same loader, so the same rules),
//   - the filter controls (`useQueryStates(historyParsers, historyOptions)` in the browser).
//
// Imported from "nuqs/server" so it also works in Server Components and Route Handlers.
//
// Every parser is a strict format or an allow-list. Anything else is treated as "not set", so
// ?member=banana, ?from=2026-02-30 or ?to=yesterday quietly fall back to the default:
// no error page, no 400 shown to the person.
import { createLoader, createParser, parseAsStringLiteral } from 'nuqs/server'
import { isRealDay } from './saRange'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** A member's id. Only the SHAPE is checked here; the server then checks they belong to THIS stokvel. */
const parseAsMemberId = createParser({
  parse: (value) => (UUID.test(value) ? value.toLowerCase() : null),
  serialize: (value) => value,
})

/**
 * A South African calendar day, "YYYY-MM-DD", that really exists. NOT nuqs's parseAsIsoDate:
 * that one turns the day into midnight UTC (02:00 in South Africa). We keep the plain text and let
 * saRange.ts, the only date converter, turn it into an instant.
 */
const parseAsDay = createParser({
  parse: (value) => (isRealDay(value) ? value : null),
  serialize: (value) => value,
})

/** The sorts the API has. The first one is the default: newest first. */
export const SORTS = ['-recordedAt', 'recordedAt', '-amount', 'amount'] as const
export type HistorySort = (typeof SORTS)[number]
export const DEFAULT_SORT: HistorySort = '-recordedAt'

export const historyParsers = {
  member: parseAsMemberId, // null = everyone
  from: parseAsDay, // null = no start
  to: parseAsDay, // null = no end
  sort: parseAsStringLiteral(SORTS).withDefault(DEFAULT_SORT),
}

/** How the controls write to the address bar. */
export const historyOptions = {
  // shallow:false asks the SERVER to render the page again with the new address (the list is rendered there).
  shallow: false,
  // A value equal to its default is left out, so the unfiltered page has a clean address.
  clearOnDefault: true,
  // 'push' adds a history entry per applied filter: the controls apply on a deliberate change (a pick, a date),
  // not on every keystroke, so Back steps through the filters the person chose instead of leaving the page.
  history: 'push' as const,
}

/** Reads the filters from a page's searchParams or from a Request URL, applying the rules above. */
export const loadHistoryParams = createLoader(historyParsers)

export type HistoryFilter = {
  member: string | null
  from: string | null
  to: string | null
  sort: HistorySort
}
