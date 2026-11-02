'use client'

import { useTransition } from 'react'
import { useQueryStates } from 'nuqs'
import { historyOptions, historyParsers, SORTS } from '@/lib/historyParams'

const SORT_LABELS: Record<(typeof SORTS)[number], string> = {
  '-recordedAt': 'Newest first',
  recordedAt: 'Oldest first',
  '-amount': 'Largest first',
  amount: 'Smallest first',
}

export type MemberOption = { userId: string; name: string }

/**
 * The filter controls. They only change the address bar (?member=&from=&to=&sort=); the SERVER then renders
 * the list again for that address. Because of that, a reload or a shared link shows the same list.
 */
export function HistoryFilters({ members }: { members: MemberOption[] }) {
  // While the server renders the page again, `isPending` is true, and we say so.
  const [isPending, startTransition] = useTransition()
  const [filter, setFilter] = useQueryStates(historyParsers, { ...historyOptions, startTransition })

  // A member id in the address that is not in this stokvel shows as "Everyone", the same as the server shows.
  const memberKnown = filter.member !== null && members.some((m) => m.userId === filter.member)
  const anyFilter = filter.member !== null || filter.from !== null || filter.to !== null || filter.sort !== historyParsers.sort.defaultValue

  const field = 'mt-1 w-full rounded-md border border-input bg-card px-3 py-2 text-sm'

  return (
    <form
      onSubmit={(event) => event.preventDefault()}
      aria-busy={isPending}
      className="grid gap-4 rounded-lg border border-border p-4 sm:grid-cols-2 lg:grid-cols-4"
    >
      <div>
        <label htmlFor="history-member" className="block text-sm font-medium">Member</label>
        <select
          id="history-member"
          value={memberKnown ? (filter.member ?? '') : ''}
          onChange={(event) => void setFilter({ member: event.target.value || null })}
          className={field}
        >
          <option value="">Everyone</option>
          {members.map((m) => (
            <option key={m.userId} value={m.userId}>{m.name}</option>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="history-from" className="block text-sm font-medium">From (South African date)</label>
        <input
          id="history-from"
          type="date"
          value={filter.from ?? ''}
          max={filter.to ?? undefined}
          onChange={(event) => void setFilter({ from: event.target.value || null })}
          className={field}
        />
      </div>

      <div>
        <label htmlFor="history-to" className="block text-sm font-medium">To (South African date)</label>
        <input
          id="history-to"
          type="date"
          value={filter.to ?? ''}
          min={filter.from ?? undefined}
          onChange={(event) => void setFilter({ to: event.target.value || null })}
          className={field}
        />
      </div>

      <div>
        <label htmlFor="history-sort" className="block text-sm font-medium">Order</label>
        <select
          id="history-sort"
          value={filter.sort}
          onChange={(event) => void setFilter({ sort: event.target.value as (typeof SORTS)[number] })}
          className={field}
        >
          {SORTS.map((sort) => (
            <option key={sort} value={sort}>{SORT_LABELS[sort]}</option>
          ))}
        </select>
      </div>

      <div className="flex flex-wrap items-center gap-4 sm:col-span-2 lg:col-span-4">
        {anyFilter && (
          <button
            type="button"
            onClick={() => void setFilter({ member: null, from: null, to: null, sort: null })}
            className="cursor-pointer text-sm underline"
          >
            Clear filters
          </button>
        )}
        {/* Tells the person the list is on its way: the server is rendering it again. */}
        <p role="status" aria-live="polite" className="text-sm text-muted-foreground">
          {isPending ? 'Updating the list…' : ''}
        </p>
      </div>
    </form>
  )
}
