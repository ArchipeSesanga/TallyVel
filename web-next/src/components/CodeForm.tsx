'use client'

import Form from 'next/form'
import { useSearchParams } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { stepUrl } from '@/lib/joinFlow'

/**
 * Step 1's form. A GET form that goes to ?code=…&step=rules.
 *
 * One detail matters for Back (WCAG 2.2 SC 3.3.7, redundant entry): the history entry for step 1 was loaded with
 * an empty input, so pressing Back twice would come back to an EMPTY step 1. Just before leaving, we rewrite
 * THIS entry's address to /join?code=<what was typed>&step=code, so Back returns to step 1 with the code in it.
 */
export function CodeForm({ code: codeFromServer }: { code: string | null }) {
  // The address is the truth for what is in the box. On Back, the page the server drew for this history entry
  // may be an old copy (drawn before the code was typed), but the address is this entry's own.
  const code = useSearchParams().get('code') ?? codeFromServer
  return (
    <Form
      action="/join"
      key={code ?? ''} // a different code means a fresh input, so Back always shows the code in the address
      className="space-y-3"
      onSubmit={(event) => {
        const typed = String(new FormData(event.currentTarget).get('code') ?? '').trim()
        if (typed !== '') window.history.replaceState(window.history.state, '', stepUrl('code', typed))
      }}
    >
      <div>
        <label htmlFor="code" className="block text-sm font-medium">Invite code</label>
        <Input id="code" name="code" defaultValue={code ?? ''} required maxLength={64} autoComplete="off" spellCheck={false}
          pattern="[A-Za-z0-9_\-]{1,64}" title="Letters, numbers, - and _ only" className="mt-1 font-mono" />
      </div>
      <input type="hidden" name="step" value="rules" />
      <Button type="submit" size="lg">Continue</Button>
    </Form>
  )
}
