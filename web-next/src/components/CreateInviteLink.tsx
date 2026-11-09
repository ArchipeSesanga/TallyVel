'use client'

import { Link as LinkIcon } from 'lucide-react'
import { useActionState, useState } from 'react'
import { createInvite, type CreateInviteState } from '@/app/inviteActions'
import { Button } from '@/components/ui/button'
import { formatDateTime } from '@/lib/dates'
import { inviteLink } from '@/lib/joinFlow'

const INITIAL: CreateInviteState = { code: null, expiresAt: null, message: null, reference: null }

/** A treasurer's "Create invite link": shows the full /join link and a copy button. The server decides who may create one. */
export function CreateInviteLink({
  stokvelId, variant = 'default', label = 'Create invite link',
}: { stokvelId: string; variant?: 'default' | 'outline'; label?: string }) {
  const [state, formAction, pending] = useActionState(createInvite, INITIAL)
  const [copied, setCopied] = useState<'yes' | 'no' | null>(null)

  // The link is built here from the address bar's own origin, so it works on any host the site runs on.
  const link = state.code === null ? null : inviteLink(window.location.origin, state.code)

  async function copy() {
    if (link === null) return
    try {
      await navigator.clipboard.writeText(link)
      setCopied('yes')
    } catch {
      setCopied('no') // clipboard blocked: the link is still on screen to select by hand
    }
  }

  return (
    <div className="space-y-3">
      <form action={formAction} onSubmit={() => setCopied(null)}>
        <input type="hidden" name="stokvelId" value={stokvelId} />
        <Button type="submit" variant={variant} disabled={pending}>
          <LinkIcon aria-hidden="true" />
          {pending ? 'Creating…' : label}
        </Button>
      </form>

      {state.message !== null && (
        <p role="alert" className="text-sm font-medium text-destructive">
          {state.message}
          {state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
        </p>
      )}

      {link !== null && state.expiresAt !== null && (
        <div className="space-y-2">
          <label htmlFor="invite-link" className="block text-sm font-medium">
            Invite link (works until {formatDateTime(state.expiresAt)})
          </label>
          <div className="flex flex-wrap gap-2">
            {/* readOnly, not disabled: it can be selected and copied by hand too. */}
            <input
              id="invite-link"
              readOnly
              value={link}
              onFocus={(e) => e.currentTarget.select()}
              className="min-h-11 min-w-0 flex-1 rounded-xl border border-input bg-card px-3 font-mono text-sm"
            />
            <Button type="button" onClick={copy}>Copy link</Button>
          </div>
          <p role="status" className="text-sm text-muted-foreground">
            {copied === 'yes' && 'Link copied.'}
            {copied === 'no' && "Couldn't copy automatically. Select the link and copy it."}
          </p>
        </div>
      )}
    </div>
  )
}
