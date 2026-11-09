import { CreditCard } from 'lucide-react'
import type { ContributionItem } from '@/api/types'
import { Avatar } from '@/components/StokvelParts'
import { ProofControls } from '@/components/ProofControls'
import { formatDateTime } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { mayChangeProof } from '@/lib/stokvelView'

/** The latest contributions, each with its proof controls. The page fetches the items; this only draws them. */
export function RecentContributionsList({
  items, stokvelId, youId, canRun, openCycleLabel, names,
}: {
  items: ContributionItem[]
  stokvelId: string
  youId: string
  /** Treasurer or Chairperson of THIS stokvel. */
  canRun: boolean
  openCycleLabel: string | null
  /** member id → name, from the stokvel's member list. */
  names: Record<string, string>
}) {
  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-input px-4 py-7 text-center">
        <CreditCard className="size-7 text-muted-foreground" strokeWidth={1.75} aria-hidden="true" />
        <p className="text-[15px] font-semibold">No contributions yet</p>
        <p className="max-w-[260px] text-sm text-muted-foreground">Payments show up here once a cycle is open and members start paying in.</p>
      </div>
    )
  }

  return (
    <ul className="divide-y divide-divider">
      {items.map((c) => {
        const you = c.memberUserId === youId
        const who = you ? 'You' : (names[c.memberUserId] ?? 'A member')
        return (
          // Avatar, name, date and amount. The id itself is never shown.
          <li key={c.id} className="py-3.5 first:pt-0 last:pb-0">
            <div className="flex items-center justify-between gap-3">
              <div className="flex min-w-0 items-center gap-3">
                <Avatar name={who} tone={you ? 'you' : 'alt'} round />
                <div className="flex min-w-0 flex-col">
                  <span className="text-base font-semibold break-words">{who}</span>
                  <span className="text-[13px] text-muted-foreground tabular-nums">
                    {formatDateTime(c.recordedAt)} · {c.status} · cycle {c.cycle}
                  </span>
                </div>
              </div>
              <span className="text-base font-semibold whitespace-nowrap tabular-nums">{formatCents(c.amountCents)}</span>
            </div>
            <ProofControls
              stokvelId={stokvelId}
              contributionId={c.id}
              hasProof={c.hasProof}
              canDownload={c.canDownloadProof}
              canChange={mayChangeProof(c, youId, canRun, openCycleLabel)}
            />
          </li>
        )
      })}
    </ul>
  )
}
