'use client'

import { useEffect, useRef, useState } from 'react'
import { useInfiniteQuery } from '@tanstack/react-query'
import { BffRequestError, fetchBffJson } from '@/lib/bffClient'

type Page<T> = { items: T[]; nextPageToken: string }

/**
 * Pages after page one. The server renders page one; this loads the rest through our own site (/bff/...)
 * when the person scrolls to the end of the list or presses "Load more".
 *
 * - `firstToken` is the token page one ended with. It is where the first extra page starts (initialPageParam).
 * - `queryKey` must hold EVERY filter value, plus `firstToken`. A different filter, or a page one that
 *   changed (a new contribution), is then a different key: it starts again and never reuses pages from another list.
 * - Nothing is fetched until the person asks, so a list nobody scrolls costs no extra request.
 */
export function usePagedList<T>(options: {
  queryKey: readonly unknown[]
  firstToken: string
  urlFor: (pageToken: string) => string
}) {
  const { queryKey, firstToken, urlFor } = options
  const [asked, setAsked] = useState(false)

  const query = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam, signal }) => fetchBffJson<Page<T>>(urlFor(pageParam), signal),
    initialPageParam: firstToken,
    // "" or null means "no more pages". Anything else is the token for the next one.
    getNextPageParam: (lastPage) => (lastPage.nextPageToken ? lastPage.nextPageToken : undefined),
    enabled: asked && firstToken !== '',
  })

  const moreItems = query.data?.pages.flatMap((page) => page.items) ?? []

  // Is there anything left? Before the first ask we only know page one's token. After it, the API's last token decides.
  // A failed first request leaves this true, so "Load more" stays available to try again.
  const hasMore = !asked ? firstToken !== '' : query.data === undefined ? true : query.hasNextPage

  const loading = query.isFetching
  const failed = query.isError ? (query.error instanceof BffRequestError ? query.error : new BffRequestError(0, 'Something went wrong.')) : null

  function loadMore() {
    if (loading || !hasMore) return
    if (!asked) setAsked(true)
    else if (query.data === undefined) void query.refetch() // the first extra page failed: try it again
    else void query.fetchNextPage()
  }

  return { moreItems, hasMore, loading, failed, loadMore }
}

/**
 * Under a list: a sentinel the browser watches (IntersectionObserver) AND a real button, so a keyboard
 * or a screen reader can load more too. The status line says how many are showing, and says "everything"
 * only once the API's token is really empty.
 */
export function LoadMoreFooter(props: {
  shown: number
  noun: string // "contributions", "events"
  hasMore: boolean
  loading: boolean
  failed: BffRequestError | null
  loadMore: () => void
}) {
  const { shown, noun, hasMore, loading, failed, loadMore } = props
  const sentinel = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const element = sentinel.current
    // After a failure we stop loading by ourselves, so an error cannot turn into a loop of requests.
    if (element === null || !hasMore || loading || failed !== null) return
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) loadMore()
      },
      { rootMargin: '200px' }, // start a little before the end is on screen
    )
    observer.observe(element)
    return () => observer.disconnect()
    // `shown` is here so the observer is created again after each page: if the end of the list is STILL on
    // screen (a tall window, a short page), it fires again and loads the next one.
  }, [hasMore, loading, failed, loadMore, shown])

  let status: string
  if (failed !== null) status = `Could not load more. ${failed.message}`
  else if (loading) status = `Loading more… ${shown} ${noun} showing so far.`
  else if (hasMore) status = `Showing ${shown} ${noun}. There are more.`
  else status = `That's everything: ${shown} ${noun}.`

  return (
    <div className="mt-4 space-y-3">
      <div ref={sentinel} aria-hidden="true" />
      <p role="status" aria-live="polite" className="text-sm text-muted-foreground">
        {status}
        {failed?.reference ? <> Reference: <code className="font-mono">{failed.reference}</code></> : null}
      </p>
      {/* Stays on screen while a page loads (disabled), so it never flickers away and back. Gone only when there is nothing more. */}
      {hasMore && (
        <button
          type="button"
          onClick={loadMore}
          disabled={loading}
          className="cursor-pointer rounded-md border border-border bg-card px-3.5 py-2 text-sm font-semibold transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60"
        >
          {loading ? 'Loading…' : failed !== null ? 'Try again' : 'Load more'}
        </button>
      )}
    </div>
  )
}
