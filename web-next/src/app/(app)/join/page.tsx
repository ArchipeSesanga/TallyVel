import { Suspense } from 'react'
import Link from 'next/link'
import Form from 'next/form'
import { redirect } from 'next/navigation'
import { JoinForm } from '@/components/JoinForm'
import { StepHeading } from '@/components/StepHeading'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { formatCents } from '@/lib/money'
import { formatDateTime } from '@/lib/dates'
import { fetchInvitePreview, kindOf, ownStokvelIdNamed, type PreviewOutcome } from '@/lib/invites'
import { JOIN_MESSAGES, decideStep, scheduleText, stepUrl, type JoinStep } from '@/lib/joinFlow'
import { loadJoinParams } from '@/lib/joinParams'
import { tryAgainIn } from '@/lib/errorMessages'
import type { InvitePreview } from '@/api/types'

type SearchParams = Promise<Record<string, string | string[] | undefined>>

const HEADINGS: Record<JoinStep, string> = {
  code: 'Step 1 of 3: Enter the invite code',
  rules: 'Step 2 of 3: Check the rules',
  confirm: 'Step 3 of 3: Confirm and join',
}
const STEP_NAMES: Record<JoinStep, string> = { code: 'Enter the code', rules: 'Check the rules', confirm: 'Confirm' }

// Reading ?code and ?step needs the request, so it all happens inside <Suspense>; the shell is static.
export default function JoinPage({ searchParams }: { searchParams: SearchParams }) {
  return (
    <Suspense fallback={<div className="h-48 animate-pulse rounded bg-muted" aria-busy="true" aria-label="Loading" />}>
      <Join searchParams={searchParams} />
    </Suspense>
  )
}

async function Join({ searchParams }: { searchParams: SearchParams }) {
  const raw = await searchParams
  const { code, step: requested } = loadJoinParams(raw)

  // A code that is not even shaped like one (spaces, slashes): keep what was typed so the person can fix it.
  const rawCode = typeof raw.code === 'string' ? raw.code.trim() : ''
  const badCode = code === null && rawCode !== ''
  const shownCode = code ?? (badCode ? rawCode.slice(0, 64) : null)

  const outcome = code === null ? null : await fetchInvitePreview(code)
  const step = decideStep(requested, code !== null, kindOf(outcome))

  // One rule keeps the address honest: if the address names a different step than the one we can show, go there.
  if (step !== requested) redirect(stepUrl(step, shownCode))

  const steps: JoinStep[] = ['code', 'rules', 'confirm']
  const valid = outcome?.kind === 'valid' ? outcome.preview : null

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <p className="text-sm text-muted-foreground">Join a stokvel</p>
      <ol aria-label="Steps" className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
        {steps.map((s, i) => (
          <li key={s} aria-current={s === step ? 'step' : undefined} className={s === step ? 'font-semibold underline' : 'text-muted-foreground'}>
            {i + 1}. {STEP_NAMES[s]}
          </li>
        ))}
      </ol>

      <StepHeading key={step}>{HEADINGS[step]}</StepHeading>

      {step === 'code' && <CodeStep code={shownCode} problem={badCode ? { kind: 'not-found' } : outcome} />}
      {step === 'rules' && valid !== null && code !== null && <RulesStep code={code} preview={valid} />}
      {step === 'confirm' && valid !== null && code !== null && <ConfirmStep code={code} preview={valid} />}
    </div>
  )
}

/** Step 1: a plain GET form. Submitting goes to ?step=rules; the page then asks the API and redirects back here if the code is no good. */
async function CodeStep({ code, problem }: { code: string | null; problem: PreviewOutcome | null }) {
  return (
    <div className="space-y-4">
      <Problem outcome={problem} />
      {/* key = the code, so Back to this step shows the code again even if the input was already on screen. */}
      <Form action="/join" key={code ?? ''} className="space-y-3">
        <input type="hidden" name="step" value="rules" />
        <div>
          <label htmlFor="code" className="block text-sm font-medium">Invite code</label>
          <Input id="code" name="code" defaultValue={code ?? ''} required maxLength={64} autoComplete="off" spellCheck={false}
            pattern="[A-Za-z0-9_\-]{1,64}" title="Letters, numbers, - and _ only" className="mt-1 font-mono" />
        </div>
        <Button type="submit" size="lg">Continue</Button>
      </Form>
    </div>
  )
}

async function Problem({ outcome }: { outcome: PreviewOutcome | null }) {
  if (outcome === null || outcome.kind === 'valid') return null
  const box = 'rounded-md border border-destructive bg-destructive-soft px-3 py-2 text-sm font-medium text-destructive'
  switch (outcome.kind) {
    case 'not-found':
      return <p role="alert" className={box}>{JOIN_MESSAGES['not-found']}</p>
    case 'expired':
      return <p role="alert" className={box}>{JOIN_MESSAGES.expired}</p>
    case 'rate-limited':
      return <p role="alert" className={box}>{tryAgainIn(outcome.retryAfter)}</p>
    case 'failed':
      return (
        <p role="alert" className={box}>
          {outcome.message}{outcome.reference !== null && <> Reference: <code className="font-mono">{outcome.reference}</code></>}
        </p>
      )
    case 'already-member': {
      const id = await ownStokvelIdNamed(outcome.stokvelName)
      return (
        <div role="status" className="space-y-1 rounded-md border border-border bg-card px-3 py-2 text-sm">
          <p className="font-medium">You&apos;re already a member</p>
          <Link href={id !== null ? `/stokvels/${id}` : '/stokvels'} prefetch={false} className="underline">
            {id !== null ? `Go to ${outcome.stokvelName}` : 'Go to your stokvels'}
          </Link>
        </div>
      )
    }
  }
}

/** The facts a non-member may see, drawn on the server. Names, balances and payout order are not in the preview at all. */
function Facts({ preview }: { preview: InvitePreview }) {
  return (
    <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
      <dt className="text-muted-foreground">Stokvel</dt>
      <dd className="font-medium break-words">{preview.stokvelName}</dd>
      <dt className="text-muted-foreground">Contribution</dt>
      <dd className="whitespace-nowrap">{formatCents(preview.contributionAmountCents)}</dd>
      <dt className="text-muted-foreground">Schedule</dt>
      <dd>{scheduleText(preview.frequency, preview.cycleLengthDays)}</dd>
    </dl>
  )
}

function RulesStep({ code, preview }: { code: string; preview: InvitePreview }) {
  return (
    <div className="space-y-5">
      <Facts preview={preview} />
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
        <dt className="text-muted-foreground">Members</dt>
        <dd>{preview.memberCount}</dd>
        <dt className="text-muted-foreground">Invite expires</dt>
        <dd>{formatDateTime(preview.expiresAt)}</dd>
      </dl>
      <section>
        <h2 className="text-lg font-semibold">Rules</h2>
        {/* Plain text with line breaks kept; React escapes it, so it can't carry markup. */}
        <p className="mt-1 whitespace-pre-wrap break-words">{preview.rules ?? 'This stokvel has not written any rules yet.'}</p>
      </section>
      <div className="flex gap-3">
        <Link href={stepUrl('code', code)} prefetch={false} className="inline-flex h-9 items-center rounded-lg border border-border px-3 text-sm font-medium">Back</Link>
        <Link href={stepUrl('confirm', code)} prefetch={false} className="inline-flex h-9 items-center rounded-lg bg-primary px-3 text-sm font-medium text-primary-foreground">Continue</Link>
      </div>
    </div>
  )
}

function ConfirmStep({ code, preview }: { code: string; preview: InvitePreview }) {
  return (
    <div className="space-y-5">
      {/* Shown again from step 2's answer, never asked for again (WCAG 2.2 SC 3.3.7). */}
      <Facts preview={preview} />
      <JoinForm code={code} stokvelName={preview.stokvelName} firstKey={crypto.randomUUID()} />
      <Link href={stepUrl('rules', code)} prefetch={false} className="inline-block text-sm underline">Back to the rules</Link>
    </div>
  )
}
