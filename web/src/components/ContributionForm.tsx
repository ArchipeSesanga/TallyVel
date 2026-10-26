import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { MeHomeEntry, ValidationProblemDetails } from '@/api/types'
import { useRecordContribution } from '@/api/queries'
import { ApiError } from '@/auth/session'
import { centsToInput, parseRandsToCents } from '@/lib/moneyInput'
import { formatCents } from '@/lib/money'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

// What a valid form looks like. The rules live here, not in the JSX.
const schema = z.object({
  amount: z
    .string()
    .refine((text) => parseRandsToCents(text) !== null, { message: 'Enter an amount like 150.50' })
    .refine((text) => (parseRandsToCents(text) ?? 0) > 0, { message: 'The amount must be more than R 0,00' }),
})

type FormValues = z.infer<typeof schema>

// One "intent to pay": the key, plus exactly what was being paid.
type Attempt = {
  key: string
  memberUserId: string
  cycle: string
  amountCents: number
}

/** Reads the correlationId from an error body, if the API sent one. */
function referenceOf(body: unknown): string {
  if (typeof body === 'object' && body !== null && 'correlationId' in body && typeof body.correlationId === 'string') {
    return ` Reference: ${body.correlationId}`
  }
  return ''
}

type ContributionFormProps = {
  entry: MeHomeEntry
  memberUserId: string
  cycleLabel: string
}

export function ContributionForm({ entry, memberUserId, cycleLabel }: ContributionFormProps) {
  const recordContribution = useRecordContribution(entry.stokvelId)

  // The key for the payment being attempted. It SURVIVES a failed attempt, so retrying
  // the same payment sends the same key, and the API can tell it's the same payment.
  const [attempt, setAttempt] = useState<Attempt | null>(null)
  const [recorded, setRecorded] = useState<string | null>(null)

  // Blocks a second submit from a fast double-click before the button has re-rendered as disabled.
  const sending = useRef(false)

  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    // Prefilled with what the member still owes, as editable text ("150.50", not "R 150,50").
    defaultValues: {
      amount: entry.owedThisCycleCents > 0 ? centsToInput(entry.owedThisCycleCents) : '',
    },
  })

  function showError(error: unknown) {
    if (!(error instanceof ApiError)) {
      // No answer at all (network dropped). It may or may not have been recorded,
      // so we KEEP the key: retrying with it can never record the money twice.
      setError('root', {
        message:
          "We couldn't reach RondiTrack. This payment may have gone through. Trying again is safe: it won't be recorded twice.",
      })
      return
    }

    const problem = error.body as ValidationProblemDetails | null
    const reference = referenceOf(error.body)

    if (error.status === 400 && problem?.errors) {
      // Put each API message under its field; anything the form doesn't know goes on top.
      for (const [field, messages] of Object.entries(problem.errors)) {
        const message = messages.join(' ')
        if (field.toLowerCase() === 'amountcents') {
          setError('amount', { message })
        } else {
          setError('root', { message: `${message}${reference}` })
        }
      }
      return
    }

    if (error.status === 409 || error.status === 422) {
      setError('root', { message: `${problem?.detail ?? 'This contribution could not be recorded.'}${reference}` })
      return
    }

    if (error.status === 429) {
      const seconds = Number(error.retryAfter)
      setError('root', {
        message: Number.isFinite(seconds)
          ? `Too many requests. Try again in ${seconds} seconds.${reference}`
          : `Too many requests. Try again in a moment.${reference}`,
      })
      return
    }

    // 5xx: the server may have saved it before failing, so we keep the key here too.
    setError('root', {
      message: `Something went wrong on our side. This payment may have gone through. Trying again is safe.${reference}`,
    })
  }

  async function onSubmit(values: FormValues) {
    if (sending.current) return
    sending.current = true

    try {
      const amountCents = parseRandsToCents(values.amount)
      if (amountCents === null) return // Zod already checked this; it keeps TypeScript happy

      // Same payment as the last failed attempt → the SAME key.
      // A different amount, member or cycle is a different payment → a NEW key.
      const samePayment =
        attempt !== null &&
        attempt.amountCents === amountCents &&
        attempt.memberUserId === memberUserId &&
        attempt.cycle === cycleLabel
      const key = samePayment ? attempt.key : crypto.randomUUID()
      setAttempt({ key, memberUserId, cycle: cycleLabel, amountCents })
      setRecorded(null)

      const saved = await recordContribution.mutateAsync({
        idempotencyKey: key,
        body: { memberUserId, cycle: cycleLabel, amountCents },
      })

      // This payment is done: forget its key, so the next contribution gets a new one.
      setAttempt(null)
      setRecorded(`Recorded ${formatCents(saved.amountCents)} from you for cycle ${saved.cycle}.`)
      reset({ amount: '' })
    } catch (error) {
      showError(error)
    } finally {
      sending.current = false
    }
  }

  return (
    <form onSubmit={(event) => void handleSubmit(onSubmit)(event)} noValidate className="space-y-4 rounded-lg border border-border p-4">
      <div>
        <h3 className="text-base font-semibold">Record your contribution</h3>
        <p className="text-sm text-muted-foreground">Cycle {cycleLabel}</p>
      </div>

      {errors.root && (
        <p role="alert" className="text-sm font-medium text-destructive">
          {errors.root.message}
        </p>
      )}

      <div className="space-y-2">
        <Label htmlFor="amount">Amount (R)</Label>
        <Input
          id="amount"
          inputMode="decimal"
          autoComplete="off"
          aria-invalid={errors.amount ? true : undefined}
          aria-describedby={errors.amount ? 'amount-error' : undefined}
          {...register('amount')}
        />
        {errors.amount && (
          <p id="amount-error" className="text-sm text-destructive">
            {errors.amount.message}
          </p>
        )}
      </div>

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Recording…' : 'Record contribution'}
      </Button>

      {recorded !== null && (
        <p role="status" className="text-sm font-medium">
          {recorded}
        </p>
      )}
    </form>
  )
}