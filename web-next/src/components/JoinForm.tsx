'use client'

import { useActionState } from 'react'
import Link from 'next/link'
import { joinStokvel, type JoinState } from '@/app/inviteActions'
import { Button } from '@/components/ui/button'
import { stepUrl } from '@/lib/joinFlow'

/**
 * Step 3's form. It receives ONLY the code, the stokvel's name (for the button) and the first key: nothing else
 * from the invite preview crosses into the browser. The rest of step 3 is drawn on the server.
 */
export function JoinForm({ code, stokvelName, firstKey }: { code: string; stokvelName: string; firstKey: string }) {
  const initial: JoinState = { message: null, next: 'retry', stokvelId: null, reference: null, idempotencyKey: firstKey }
  const [state, formAction, pending] = useActionState(joinStokvel, initial)

  return (
    <form action={formAction} className="space-y-4">
      <input type="hidden" name="code" value={code} />
      {/* The server wrote this key. It stays the same for a retry and only changes when the server says so. */}
      <input type="hidden" name="idempotencyKey" value={state.idempotencyKey} />

      {state.message !== null && (
        <div role="alert" className="space-y-2 rounded-md border border-destructive bg-destructive-soft px-3 py-2 text-sm font-medium text-destructive">
          <p>
            {state.message}
            {state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
          </p>
          {state.next === 'already-member' && (
            <p>
              <Link
                href={state.stokvelId !== null ? `/stokvels/${state.stokvelId}` : '/stokvels'}
                prefetch={false}
                className="underline"
              >
                {state.stokvelId !== null ? `Go to ${stokvelName}` : 'Go to your stokvels'}
              </Link>
            </p>
          )}
          {state.next === 'start-again' && (
            <p>
              <Link href={stepUrl('code', code)} prefetch={false} className="underline">Back to step 1</Link>
            </p>
          )}
        </div>
      )}

      {/* Disabled while the request runs: one click is one join. (The key makes a second click harmless anyway.) */}
      <Button type="submit" size="lg" disabled={pending}>
        {pending ? 'Joining…' : `Join ${stokvelName}`}
      </Button>
    </form>
  )
}
