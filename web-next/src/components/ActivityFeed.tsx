'use client'

import type { ActivityItem, ActivityKind, ActivityPage } from '@/api/types'
import { LoadMoreFooter, usePagedList } from '@/components/LoadMore'
import { formatCents } from '@/lib/money'
import { formatDateTime } from '@/lib/dates'

// The API sends the name of the kind; people read a sentence. Keyed by the generated ActivityKind,
// so a new kind in the API will not compile until it has words here.
const KIND_WORDS: Record<ActivityKind, string> = {
  ContributionRecorded: 'contributed',
  PayoutMade: 'was paid out',
  MemberJoined: 'joined',
}

function ActivityRow({ item }: { item: ActivityItem }) {
  const who = item.personName ?? 'A member'
  return (
    <li className="flex flex-wrap items-baseline justify-between gap-2 p-3">
      <span>
        <span className="font-medium">{who}</span> {KIND_WORDS[item.kind]}
        {item.amountCents !== null && <> <span className="font-medium whitespace-nowrap">{formatCents(item.amountCents)}</span></>}
        {' '}in {item.stokvelName}
      </span>
      <span className="text-sm text-muted-foreground">{formatDateTime(item.occurredAt)}</span>
    </li>
  )
}

/** The activity feed: page one from the server, the rest through /bff/activity. */
export function ActivityFeed({ initial }: { initial: ActivityPage }) {
  const { moreItems, hasMore, loading, failed, loadMore } = usePagedList<ActivityItem>({
    queryKey: ['activity', initial.nextPageToken],
    firstToken: initial.nextPageToken,
    urlFor: (pageToken) => `/bff/activity?pageToken=${encodeURIComponent(pageToken)}`,
  })
  const items = [...initial.items, ...moreItems]

  return (
    <div>
      <ul className="divide-y divide-border rounded-lg border border-border">
        {items.map((item) => (
          <ActivityRow key={item.id} item={item} />
        ))}
      </ul>
      <LoadMoreFooter shown={items.length} noun="events" hasMore={hasMore} loading={loading} failed={failed} loadMore={loadMore} />
    </div>
  )
}
