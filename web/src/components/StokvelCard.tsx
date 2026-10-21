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
    <article className="card">
      <h2>{entry.name}</h2>
      <p className="role">{roleLabel(entry.role)}</p>

      {currentCycle === null ? (
        // No open cycle: show no paid/owed figures at all (the API sends 0 for both,
        // and "R 0,00 owed" would wrongly look like good news).
        <p>No open cycle</p>
      ) : (
        <dl>
          <dt>Cycle</dt>
          <dd>{currentCycle.label}</dd>

          <dt>Status</dt>
          <dd>{cycleStatusLabels[currentCycle.status]}</dd>

          <dt>Due</dt>
          <dd>{currentCycle.endsOn === null ? 'No due date' : formatDate(currentCycle.endsOn)}</dd>

          {/* Money is only ever shown with formatCents, never divided or rounded here.
              Paid and owed are worked out by the API. */}
          <dt>Paid this cycle</dt>
          <dd>{formatCents(entry.paidThisCycleCents)}</dd>

          <dt>Still owed</dt>
          {/* Derived in the render, not stored in state. It is an explicit === 0 comparison
              so it can never leak a stray "0" onto the screen the way `count && ...` can. */}
          <dd>
            {entry.owedThisCycleCents === 0
              ? 'All paid up for this cycle'
              : formatCents(entry.owedThisCycleCents)}
          </dd>

          <dt>Next payout</dt>
          <dd>{entry.nextPayoutDate === null ? 'No payout scheduled' : formatDate(entry.nextPayoutDate)}</dd>
        </dl>
      )}

      {/* Hiding this is only about what to show. The API still refuses a Member with a 403. */}
      {canRunCycle(entry.role) && (
        <section className="tasks" aria-label="Treasurer tasks">
          <h3>Treasurer tasks</h3>
          <button type="button" disabled>
            Close cycle
          </button>
          <button type="button" disabled>
            Record payout
          </button>
          <p className="note">Coming in Week 8</p>
        </section>
      )}
    </article>
  )
}
