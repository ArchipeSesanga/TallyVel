'use client'

import { useRouter } from 'next/navigation'
import { GroupOverview } from '@/components/GroupOverview'
import type { Group } from '@/lib/groupOverview'

/**
 * The dashboard's groups. A Server Component can't hand a function to GroupOverview, so this small client
 * wrapper supplies the click behaviour. For now "Record payout" opens the group's page (the payout screen
 * itself is not built yet); it is the one place to change when it is.
 */
export function MemberGroups({ groups, today }: { groups: Group[]; today: string }) {
  const router = useRouter()
  return (
    <GroupOverview
      groups={groups}
      today={today}
      hrefFor={(id) => `/stokvels/${id}`}
      onRecordPayout={(id) => router.push(`/stokvels/${id}`)}
    />
  )
}
