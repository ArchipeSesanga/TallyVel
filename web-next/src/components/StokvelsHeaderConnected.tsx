'use client'

import { useRouter } from 'next/navigation'
import { checkInviteCode } from '@/app/inviteActions'
import { StokvelsHeader } from '@/components/StokvelsHeader'
import { JoinError } from '@/lib/joinErrors'
import { stepUrl } from '@/lib/joinFlow'
import type { Group } from '@/lib/groupOverview'

/**
 * Connects the header's code box to the real join flow. A good code goes to step 2 of /join (the rules), where the
 * person confirms. The same /join page still handles invite links opened from WhatsApp or SMS (?code=…).
 */
export function StokvelsHeaderConnected({ groups }: { groups: Group[] }) {
  const router = useRouter()
  return (
    <StokvelsHeader
      groups={groups}
      onJoin={async (code) => {
        const result = await checkInviteCode(code)
        if (!result.ok) throw new JoinError(result.kind, result.detail)
        router.push(stepUrl('rules', code))
      }}
    />
  )
}
