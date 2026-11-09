'use client'

import Form from 'next/form'
import { useSearchParams } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { extractCode, stepUrl } from '@/lib/joinFlow'

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
        // A pasted link becomes its code before the form reads it.
        const box = event.currentTarget.elements.namedItem('code') as HTMLInputElement
        box.value = extractCode(box.value)
        const typed = box.value
        if (typed !== '') window.history.replaceState(window.history.state, '', stepUrl('code', typed))
      }}
    >
      <div>
        <label htmlFor="code" className="block text-sm font-medium">Invite code</label>
        {/* No pattern= and no maxLength: a pasted link is longer than a code and is turned into the code on paste
            and again on submit. The page and the API judge the code itself. */}
        <Input id="code" name="code" defaultValue={code ?? ''} required autoComplete="off" spellCheck={false}
          onPaste={(e) => {
            // Pasting a whole link puts just its code in the box. Typing is left alone.
            const pasted = e.clipboardData.getData('text')
            const code = extractCode(pasted)
            if (code !== pasted.trim()) {
              e.preventDefault()
              e.currentTarget.value = code
            }
          }}
          className="mt-1 font-mono" />
        <p className="mt-1 text-sm text-muted-foreground">You can paste the whole invite link here.</p>
      </div>
      <input type="hidden" name="step" value="rules" />
      <Button type="submit" size="lg">Continue</Button>
    </Form>
  )
}
