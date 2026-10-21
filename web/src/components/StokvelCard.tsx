import type { CycleStatus, MeHomeEntry } from '../api/types'
import { formatCents } from '../lib/money'
import { formatDate } from '../lib/dates'
import { canRunCycle, roleLabel } from '../lib/roles'

// The API sends "PaidOut"; people read "Paid out". Keyed by the generated CycleStatus,
// so a new status in the API will not compile until it has a label here.
const cycleStatusLabels: Record<CycleStatus, string> = {
  Open: 'Open',
  Closed: 'Closed',
  PaidOut: 'Paid out',
}

type StokvelCardProps = {
  // Everything the card shows comes from this one /me/home entry, role included.
  // That is why two cards for the same person can differ: each has its own entry.
  entry: MeHomeEntry
}

export function StokvelCard({ entry }: StokvelCardProps) {
  const { currentCycle } = entry

  return (
    <article className="w-full overflow-hidden rounded-lg border border-border bg-card text-card-foreground shadow-sm">
      <div className="p-4 sm:p-6">
        {/* break-words: a long name like "Mokoena Family Burial and Savings Society (Soweto)"
            wraps instead of pushing the card wider than a 360px phone. */}
        <h3 className="text-base font-semibold break-words">{entry.name}</h3>
        <p className="mt-1 text-sm text-muted-foreground">{roleLabel(entry.role)}</p>

        {currentCycle === null ? (
          // No open cycle: show no paid/owed figures at all (the API sends 0 for both,
          // and "R 0,00 owed" would wrongly look like good news).
          <p className="mt-4 text-sm text-muted-foreground">No open cycle</p>
        ) : (
          <dl className="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <dt className="text-muted-foreground">Cycle</dt>
            <dd>{currentCycle.label}</dd>

            <dt className="text-muted-foreground">Status</dt>
            <dd>{cycleStatusLabels[currentCycle.status]}</dd>

            <dt className="text-muted-foreground">Due</dt>
            <dd>{currentCycle.endsOn === null ? 'No due date' : formatDate(currentCycle.endsOn)}</dd>

            {/* Money is only ever shown with formatCents, never divided or rounded here.
                Paid and owed are worked out by the API.
                whitespace-nowrap: "R 12 345,67" never splits over two lines. */}
            <dt className="text-muted-foreground">Paid this cycle</dt>
            <dd className="font-medium whitespace-nowrap">{formatCents(entry.paidThisCycleCents)}</dd>

            <dt className="text-muted-foreground">Still owed</dt>
            {/* Derived in the render, not stored in state. It is an explicit === 0 comparison
                so it can never leak a stray "0" onto the screen the way `count && ...` can. */}
            <dd className="font-medium">
              {entry.owedThisCycleCents === 0 ? (
                'All paid up for this cycle'
              ) : (
                <span className="whitespace-nowrap">{formatCents(entry.owedThisCycleCents)}</span>
              )}
            </dd>

            <dt className="text-muted-foreground">Next payout</dt>
            <dd>{entry.nextPayoutDate === null ? 'No payout scheduled' : formatDate(entry.nextPayoutDate)}</dd>
          </dl>
        )}

        {/* Hiding this is only about what to show. The API still refuses a Member with a 403. */}
        {canRunCycle(entry.role) && (
          <section className="mt-6 border-t border-border pt-4" aria-label="Treasurer tasks">
            <h4 className="text-sm font-semibold">Treasurer tasks</h4>
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                type="button"
                disabled
                className="rounded-md bg-primary px-3.5 py-2 text-sm font-semibold text-primary-foreground transition-all hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50"
              >
                Close cycle
              </button>
              <button
                type="button"
                disabled
                className="rounded-md bg-primary px-3.5 py-2 text-sm font-semibold text-primary-foreground transition-all hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50"
              >
                Record payout
              </button>
            </div>
            <p className="mt-2 text-xs text-muted-foreground">Coming in Week 8</p>
          </section>
        )}
      </div>
    </article>
  )
}