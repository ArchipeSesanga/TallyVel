import type { ContributionItem } from '../api/types'
import { formatCents } from '../lib/money'
import { formatDateTime } from '../lib/dates'

type ContributionCardProps = {
  contribution: ContributionItem
}

// The API also sends memberUserId, but it is only an id, so showing it would add nothing.
export function ContributionCard({ contribution }: ContributionCardProps) {
  return (
    <article className="card contribution">
      {/* Money only goes through formatCents: it works on whole cents, so there is no
          rounding or dividing anywhere in the frontend. */}
      <h3>{formatCents(contribution.amountCents)}</h3>
      <dl>
        {/* The status is plain text. A colour may support it, but never carries it alone. */}
        <dt>Status</dt>
        <dd className={`status status-${contribution.status.toLowerCase()}`}>{contribution.status}</dd>

        {/* formatDateTime shows the South African date AND time of the UTC instant. */}
        <dt>Paid</dt>
        <dd>{formatDateTime(contribution.recordedAt)}</dd>

        <dt>Cycle</dt>
        <dd>{contribution.cycle}</dd>
      </dl>
    </article>
  )
}
