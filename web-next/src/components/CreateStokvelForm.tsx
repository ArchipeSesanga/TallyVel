'use client'

import { useActionState } from 'react'
import { createStokvel, type CreateStokvelState } from '@/app/stokvelActions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { FREQUENCIES, FREQUENCY_LABELS, NAME_MAX, RULES_MAX } from '@/lib/stokvelSchema'

const INITIAL: CreateStokvelState = { message: null, reference: null, fieldErrors: {}, values: { name: '', amount: '', frequency: 'Monthly', rules: '' } }

const fieldClass = 'mt-1 w-full rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive md:text-sm'

/** "Create a stokvel". The person who creates it becomes its Chairperson (the API decides that). */
export function CreateStokvelForm() {
  const [state, formAction, pending] = useActionState(createStokvel, INITIAL)
  const errors = (field: string) => state.fieldErrors[field] ?? []

  return (
    <form action={formAction} className="max-w-md space-y-4" noValidate>
      {state.message !== null && (
        <p role="alert" className="rounded-md border border-destructive bg-destructive-soft px-3 py-2 text-sm font-medium text-destructive">
          {state.message}
          {state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
        </p>
      )}

      <div>
        <label htmlFor="name" className="block text-sm font-medium">Name</label>
        <Input id="name" name="name" defaultValue={state.values.name} maxLength={NAME_MAX} autoComplete="off" required
          aria-invalid={errors('name').length > 0} aria-describedby="name-error" className="mt-1" />
        <p id="name-error" role="alert" className="text-sm text-destructive">{errors('name').join(' ')}</p>
      </div>

      <div>
        <label htmlFor="amount" className="block text-sm font-medium">Contribution per member (rands)</label>
        <Input id="amount" name="amount" defaultValue={state.values.amount} inputMode="decimal" autoComplete="off" required
          aria-invalid={errors('amount').length > 0} aria-describedby="amount-error" className="mt-1" />
        <p id="amount-error" role="alert" className="text-sm text-destructive">{errors('amount').join(' ')}</p>
      </div>

      <div>
        <label htmlFor="frequency" className="block text-sm font-medium">How often members pay</label>
        <select id="frequency" name="frequency" defaultValue={state.values.frequency} aria-invalid={errors('frequency').length > 0}
          aria-describedby="frequency-error" className={fieldClass}>
          {FREQUENCIES.map((f) => <option key={f} value={f}>{FREQUENCY_LABELS[f]}</option>)}
        </select>
        <p id="frequency-error" role="alert" className="text-sm text-destructive">{errors('frequency').join(' ')}</p>
      </div>

      <div>
        <label htmlFor="rules" className="block text-sm font-medium">Rules <span className="font-normal text-muted-foreground">(optional)</span></label>
        <textarea id="rules" name="rules" defaultValue={state.values.rules} rows={5} maxLength={RULES_MAX}
          aria-invalid={errors('rules').length > 0} aria-describedby="rules-error" className={fieldClass} />
        <p id="rules-error" role="alert" className="text-sm text-destructive">{errors('rules').join(' ')}</p>
      </div>

      <p className="text-sm text-muted-foreground">You will be the stokvel&apos;s Chairperson.</p>
      {/* Disabled while the request runs, so one click makes one stokvel. */}
      <Button type="submit" size="lg" disabled={pending}>{pending ? 'Creating…' : 'Create stokvel'}</Button>
    </form>
  )
}
