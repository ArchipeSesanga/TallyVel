import { Suspense } from 'react'
import type { Metadata } from 'next'
import Link from 'next/link'
import { redirect } from 'next/navigation'
import { CodeForm } from '@/components/CodeForm'
import { JoinForm } from '@/components/JoinForm'
import { StepHeading } from '@/components/StepHeading'
import { formatCents } from '@/lib/money'
import { formatDateTime } from '@/lib/dates'
import { fetchInvitePreview, kindOf, ownStokvelIdNamed, type PreviewOutcome } from '@/lib/invites'
import { JOIN_MESSAGES, decideStep, scheduleText, stepUrl, type JoinStep } from '@/lib/joinFlow'
import { loadJoinParams } from '@/lib/joinParams'
import { loginPath } from '@/lib/loginPath'
import { getSession } from '@/lib/session'
import { inviteMetadata } from '@/lib/siteMetadata'
import { tryAgainIn } from '@/lib/errorMessages'
import type { InvitePreview } from '@/api/types'

type SearchParams = Promise<Record<string, string | string[] | undefined>>

/**
 * The invite page's <head>: what a link preview (WhatsApp, Telegram, Slack...) and a search engine may see.
 *
 * It takes NO arguments, on purpose. It is never given the invite code (searchParams) and never calls the API, so it
 * CANNOT put the stokvel's name or the contribution in the head, whoever asks. (The API's preview needs a signed-in
 * person, and a preview bot is not one.) It also says noindex: this page is for one invitee, not for a search engine.
 * It does not say whether the invite is still valid: only a signed-in request can find that out, and the head must
 * be the same for everyone.
 */
export function generateMetadata(): Metadata {
  return inviteMetadata()
}

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
  // Not signed in (a person who has not signed in yet, or a link-preview bot): the page answers 200 and shows what a
  // stranger may see. The only API call this page makes needs a session, so nothing about the stokvel is read at all.
  if ((await getSession()) === null) return <SignedOutInvite signInPath={await loginPath()} />

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
function CodeStep({ code, problem }: { code: string | null; problem: PreviewOutcome | null }) {
  return (
    <div className="space-y-4">
      <Problem outcome={problem} />
      <CodeForm code={code} />
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

/**
 * What a signed-out visitor (and a link-preview bot) gets: no stokvel name, no amount, no rules, only a way in.
 * `signInPath` is /login?next=<this very address>, so after signing in the person comes straight back to the invite.
 */
function SignedOutInvite({ signInPath }: { signInPath: string }) {
  return (
    <main className="mx-auto flex max-w-md flex-col gap-5 px-4 py-12">
      <p className="text-sm font-semibold text-muted-foreground">RondiTrack</p>
      <h1 className="text-2xl font-bold">You have been invited to join a stokvel</h1>
      <p>Sign in to see who invited you, what the contribution is and the rules, and then join. It takes a minute.</p>
      <div className="flex flex-wrap items-center gap-3">
        <Link href={signInPath} className="inline-flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground">Sign in to continue</Link>
        <Link href="/register" className="inline-flex min-h-6 items-center text-sm font-medium underline">Create an account</Link>
      </div>
      <p className="text-sm text-muted-foreground">New here? Create an account, then open your invite link again.</p>
    </main>
  )
}
