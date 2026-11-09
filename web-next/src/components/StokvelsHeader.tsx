'use client'

import Link from 'next/link'
import { useState } from 'react'
import { JoinError, joinErrorMessage } from '@/lib/joinErrors'
import { extractCode } from '@/lib/joinFlow'
import { summaryOf, type Group } from '@/lib/groupOverview'

const FOCUS = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-paid focus-visible:ring-offset-2'

type Props = {
  groups: Group[]
  /** Takes the code to its next step. Rejects with a JoinError (shown under the field); resolving means "go on". */
  onJoin: (code: string) => Promise<void>
}

/** The "My stokvels" header: title, one-line summary, an invite-code box and "Create a stokvel". */
export function StokvelsHeader({ groups, onJoin }: Props) {
  const [code, setCode] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    // A pasted invite link works too: the code is taken out of it. Whitespace is trimmed either way.
    const cleaned = extractCode(code)
    if (cleaned === '' || pending) return
    setPending(true)
    setError(null)
    try {
      await onJoin(cleaned)
    } catch (e) {
      // Anything that is not a JoinError means the request itself did not get through.
      setError(joinErrorMessage(e instanceof JoinError ? e : new JoinError('unreachable')))
    } finally {
      setPending(false)
    }
  }

  // On wider screens the error is positioned under the field without taking part in the layout, so the buttons
  // beside it do not jump when it appears; the header makes room for it instead (sm:pb-12).
  return (
    <header className={`flex flex-wrap items-end justify-between gap-4 pb-2 ${error !== null ? 'sm:pb-12' : ''}`}>
      <div className="flex flex-col gap-1">
        <h1 className="text-[32px] font-bold tracking-[-0.02em] text-ink">My stokvels</h1>
        <p className="text-[15px] text-ink-muted">{groups.length === 0 ? "You're not in any stokvels yet" : summaryOf(groups)}</p>
      </div>

      <div className="flex items-end gap-5 max-sm:w-full max-sm:flex-col max-sm:items-stretch max-sm:gap-3">
        <form onSubmit={submit} className="relative flex flex-col gap-1.5 max-sm:w-full">
          <label htmlFor="invite-code" className="text-[13px] font-medium text-label">Have an invite code?</label>
          {/* One joined field. The ring is on the group (focus-within), so it shows for the box and the button and is not clipped. */}
          <div className="flex overflow-hidden rounded-xl border border-field-border bg-white focus-within:ring-2 focus-within:ring-paid focus-within:ring-offset-2">
            <input
              id="invite-code"
              type="text"
              placeholder="Enter code"
              value={code}
              onChange={(e) => { setCode(e.target.value); setError(null) }}
              autoComplete="off"
              // Not "characters": invite codes are case-sensitive, so capitals made by a phone keyboard would break them.
              autoCapitalize="none"
              spellCheck={false}
              aria-invalid={error !== null ? true : undefined}
              aria-describedby={error !== null ? 'invite-code-error' : undefined}
              className="min-h-11 w-[150px] border-0 bg-transparent px-3.5 text-[15px] text-ink focus:outline-none max-sm:w-auto max-sm:flex-1"
            />
            <button
              type="submit"
              disabled={code.trim() === '' || pending}
              className="min-h-11 border-l border-field-border bg-head px-4 text-[15px] font-semibold text-ink focus:outline-none enabled:cursor-pointer disabled:text-ink-muted"
            >
              {pending ? 'Joining…' : 'Join'}
            </button>
          </div>
          {error !== null && (
            <p id="invite-code-error" role="alert" className="text-[13px] text-due sm:absolute sm:top-full sm:left-0 sm:mt-1.5 sm:w-[260px]">{error}</p>
          )}
        </form>

        <span aria-hidden="true" className="h-11 w-px bg-field-border max-sm:hidden" />

        <Link
          href="/stokvels/new"
          className={`inline-flex min-h-[46px] items-center gap-2 rounded-xl bg-forest px-[18px] text-[15px] font-semibold text-white no-underline max-sm:justify-center ${FOCUS}`}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M12 5v14M5 12h14" />
          </svg>
          Create a stokvel
        </Link>
      </div>
    </header>
  )
}
