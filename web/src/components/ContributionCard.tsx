import type { ContributionItem } from '../api/types'
import { formatCents } from '../lib/money'
import { formatDateTime } from '../lib/dates'

type ContributionCardProps = {
  contribution: ContributionItem
}

// The API also sends memberUserId, but it is only an id, so showing it would add nothing.
export function ContributionCard({ contribution }: ContributionCardProps) {
  return (
    <article className="w-full overflow-hidden rounded-lg border border-border bg-card p-4 text-card-foreground shadow-sm sm:p-6">
      {/* Money only goes through formatCents: it works on whole cents, so there is no
          rounding or dividing anywhere in the frontend. */}
      <h3 className="text-base font-semibold whitespace-nowrap">{formatCents(contribution.amountCents)}</h3>
      <dl className="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
        {/* The status is plain text. A colour may support it, but never carries it alone. */}
        <dt className="text-muted-foreground">Status</dt>
        <dd className={contribution.status === 'Confirmed' ? 'font-semibold text-primary' : 'font-semibold'}>{contribution.status}</dd>

        {/* formatDateTime shows the South African date AND time of the UTC instant. */}
        <dt className="text-muted-foreground">Paid</dt>
        <dd>{formatDateTime(contribution.recordedAt)}</dd>

        <dt className="text-muted-foreground">Cycle</dt>
        <dd>{contribution.cycle}</dd>
      </dl>
    </article>
  )
}
