'use client'

import type { ContributionItem, ContributionPage } from '@/api/types'
import type { HistoryFilter } from '@/lib/historyParams'
import { ContributionCard } from '@/components/ContributionCard'
import { LoadMoreFooter, usePagedList } from '@/components/LoadMore'

/** The history list: page one comes from the server, the rest through /bff/. */
export function ContributionHistory({
  stokvelId,
  filter,
  initial,
}: {
  stokvelId: string
  filter: HistoryFilter
  initial: ContributionPage
}) {
  const { moreItems, hasMore, loading, failed, loadMore } = usePagedList<ContributionItem>({
    // Every filter value is in the key, and so is the token page one ended with.
    queryKey: ['contribution-history', stokvelId, filter, initial.nextPageToken],
    firstToken: initial.nextPageToken,
    urlFor: (pageToken) => {
      const params = new URLSearchParams()
      if (filter.member !== null) params.set('member', filter.member)
      if (filter.from !== null) params.set('from', filter.from)
      if (filter.to !== null) params.set('to', filter.to)
      params.set('sort', filter.sort)
      params.set('pageToken', pageToken) // made under THIS filter: it is page one's own token, or one this filter produced
      return `/bff/stokvels/${stokvelId}/contributions?${params}`
    },
  })

  const items = [...initial.items, ...moreItems]

  return (
    <div>
      <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {items.map((c) => (
          <li key={c.id}>
            {/* Never shows memberUserId: it is only an id. */}
            <ContributionCard contribution={c} />
          </li>
        ))}
      </ul>
      <LoadMoreFooter shown={items.length} noun="contributions" hasMore={hasMore} loading={loading} failed={failed} loadMore={loadMore} />
    </div>
  )
}
