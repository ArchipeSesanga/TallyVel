'use client'

import { useActionState } from 'react'
import { recordContribution, type ContributionState } from '@/app/actions'
import { formatCents } from '@/lib/money'

export type MemberOption = { userId: string; name: string }

type ContributionFormProps = {
  stokvelId: string
  /** The open cycle's label. Written by the server; the person can't edit it. */
  cycle: string
  /** The signed-in member. */
  youId: string
  /** Everyone in this stokvel, only for someone who may record for others (THIS stokvel's role, from the API). Null: just yourself. */
  members: MemberOption[] | null
  /** What the member still owes, as text the person can edit, e.g. "150.50". Empty if nothing is owed. */
  prefill: string
  /** The first Idempotency-Key. The server wrote it; after each attempt the action's state carries the next one. */
  firstKey: string
}

export function ContributionForm({ stokvelId, cycle, youId, members, prefill, firstKey }: ContributionFormProps) {
  const initialState: ContributionState = {
    message: null,
    reference: null,
    fieldErrors: {},
    values: { memberUserId: youId, amount: prefill },
    idempotencyKey: firstKey,
    keyBody: null,
    confirmation: null,
  }
  const [state, formAction, pending] = useActionState(recordContribution, initialState)
  const errorsFor = (field: string) => state.fieldErrors[field] ?? []

  const nameOf = (userId: string) =>
    userId === youId ? 'you' : (members?.find((m) => m.userId === userId)?.name ?? 'this member')

  return (
    <form action={formAction} className="max-w-md space-y-4" noValidate>
      {/* The server writes these. The key changes only when the action's state says so. */}
      <input type="hidden" name="stokvelId" value={stokvelId} />
      <input type="hidden" name="cycle" value={cycle} />
      <input type="hidden" name="idempotencyKey" value={state.idempotencyKey} />

      {state.confirmation !== null && (
        <p role="status" className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium">
          Recorded {formatCents(state.confirmation.amountCents)} for {nameOf(state.confirmation.memberUserId)} in cycle{' '}
          {state.confirmation.cycle}.
        </p>
      )}

      {/* role="alert" makes screen readers read the error out as soon as it appears */}
      {state.message !== null && (
        <p role="alert" className="rounded-md border border-destructive bg-destructive-soft px-3 py-2 text-sm font-medium text-destructive">
          {state.message}
          {state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
        </p>
      )}
      {state.message === null && state.reference !== null && (
        <p className="text-sm text-muted-foreground">Reference: <code className="font-mono">{state.reference}</code></p>
      )}

      {members === null ? (
        <input type="hidden" name="memberUserId" value={youId} />
      ) : (
        <div>
          <label htmlFor="memberUserId" className="block text-sm font-medium">Payment for</label>
          <select
            id="memberUserId"
            name="memberUserId"
            defaultValue={state.values.memberUserId}
            aria-invalid={errorsFor('memberUserId').length > 0 ? true : undefined}
            aria-describedby="memberUserId-error"
            className="mt-1 w-full rounded-md border border-input bg-card px-3 py-2"
          >
            {members.map((m) => (
              <option key={m.userId} value={m.userId}>
                {m.userId === youId ? `${m.name} (you)` : m.name}
              </option>
            ))}
          </select>
          <p id="memberUserId-error" className="text-sm text-destructive">{errorsFor('memberUserId').join(' ')}</p>
        </div>
      )}

      <div>
        <label htmlFor="amount" className="block text-sm font-medium">Amount (rands)</label>
        {/* inputMode="decimal" brings up a number pad on a phone; type="text" so "150,50" and spaces still reach the server */}
        <input
          id="amount"
          name="amount"
          inputMode="decimal"
          autoComplete="off"
          defaultValue={state.values.amount}
          aria-invalid={errorsFor('amount').length > 0 ? true : undefined}
          aria-describedby="amount-error"
          className="mt-1 w-full rounded-md border border-input bg-card px-3 py-2"
        />
        <p id="amount-error" className="text-sm text-destructive">{errorsFor('amount').join(' ')}</p>
      </div>

      {/* Disabled while the request runs: one click, one request. */}
      <button
        type="submit"
        disabled={pending}
        className="w-full cursor-pointer rounded-md border border-primary bg-primary px-3.5 py-2 text-sm font-semibold text-primary-foreground transition-all hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60"
      >
        {pending ? 'Recording…' : 'Record contribution'}
      </button>
    </form>
  )
}
