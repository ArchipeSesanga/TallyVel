// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { ContributionForm } from './ContributionForm'
import { recordContributionCore, type ApiAnswer, type ContributionState, type RecordPort } from '@/lib/recordContribution'

// The real form, with the real contribution rules, and a FAKE API behind them. The fake sits at the one edge the
// form has: the Server Action. (The form's other edges are not used here: it doesn't navigate and has no URL state.)

const STOKVEL = '4ce16b73-25dd-4a88-b36c-e8b61a24bd65'
const LERATO = 'c67160fb-d6f0-40de-96e7-261823fef0f9'
const SIPHO = '0b9a7f68-5b5a-4a53-9a43-3c1d3e2a6f10'
const FIRST_KEY = '11111111-1111-4111-8111-111111111111'

type Sent = Parameters<RecordPort['send']>[0]

const recorded: ApiAnswer = { status: 201, recorded: true, problem: undefined, correlationId: null, retryAfter: null }
const refusal = (status: number, problem: object = {}, correlationId: string | null = 'corr-1'): ApiAnswer =>
  ({ status, recorded: false, problem, correlationId, retryAfter: null })

/**
 * A fake API. Each request waits until the test says how to answer it (answerNext), so the test controls time and
 * never has to sleep. `sent` is every request the API received, in order.
 */
function fakeApi() {
  const sent: Sent[] = []
  const waiting: ((answer: ApiAnswer | null) => void)[] = []
  let keyCounter = 0
  const port: RecordPort = {
    send: (request) => new Promise((resolve) => { sent.push(request); waiting.push(resolve) }),
    newKey: () => `99999999-9999-4999-8999-${String(++keyCounter).padStart(12, '0')}`,
    onRecorded: () => {},
  }
  const action = (previous: ContributionState, formData: FormData) => recordContributionCore(previous, formData, port)
  return { sent, action, answerNext: (answer: ApiAnswer | null) => waiting.shift()!(answer) }
}

type Props = Partial<React.ComponentProps<typeof ContributionForm>>

function renderForm(action: React.ComponentProps<typeof ContributionForm>['action'], props: Props = {}) {
  const user = userEvent.setup()
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ContributionForm stokvelId={STOKVEL} cycle="2026-11" youId={LERATO} members={null} prefill="" firstKey={FIRST_KEY} action={action} {...props} />
    </QueryClientProvider>,
  )
  return user
}

const amountField = () => screen.getByLabelText('Amount (rands)')
const recordButton = () => screen.getByRole('button', { name: 'Record contribution' })

async function typeAmountAndSend(user: ReturnType<typeof userEvent.setup>, amount: string) {
  await user.clear(amountField())
  await user.type(amountField(), amount)
  await user.click(recordButton())
}

describe('a message under the right field for each API 400', () => {
  it('the API says "amountCents"; the form\'s field is "Amount (rands)", and the message is described under it', async () => {
    const api = fakeApi()
    const user = renderForm(api.action)

    await typeAmountAndSend(user, '150,50')
    api.answerNext(refusal(400, { errors: { amountCents: ['Contribution amount must be bigger than 0 cents'] } }))

    // The alert is new (it arrives with the answer), so wait for IT, then look at what the field says about itself.
    await screen.findByText('Contribution amount must be bigger than 0 cents')
    expect(amountField()).toHaveAccessibleDescription('Contribution amount must be bigger than 0 cents')
    expect(amountField()).toBeInvalid()
  })

  it('a problem with who the payment is for is described under "Payment for"', async () => {
    const api = fakeApi()
    const user = renderForm(api.action, {
      members: [{ userId: LERATO, name: 'Lerato Mokoena' }, { userId: SIPHO, name: 'Sipho Zulu' }],
    })

    await user.selectOptions(screen.getByLabelText('Payment for'), SIPHO)
    await typeAmountAndSend(user, '100')
    api.answerNext(refusal(400, { errors: { memberUserId: ['That member is not in this stokvel.'] } }))

    await screen.findByText('That member is not in this stokvel.')
    expect(screen.getByLabelText('Payment for')).toHaveAccessibleDescription('That member is not in this stokvel.')
    expect(amountField()).not.toBeInvalid() // the other field is not blamed
  })

  it('an amount the browser-side rules refuse never reaches the API, and says why under the field', async () => {
    const api = fakeApi()
    const user = renderForm(api.action)

    await typeAmountAndSend(user, '10,555')

    await screen.findByText(/no more than two decimals/)
    expect(api.sent).toHaveLength(0)
    expect(amountField()).toHaveAccessibleDescription(/no more than two decimals/)
  })
})

describe('a refusal that is about the payment as a whole', () => {
  it.each([
    [409, 'A contribution has already been recorded for this member in cycle 2026-11.', 'ref-409'],
    [422, 'That member is not part of this stokvel.', 'ref-422'],
  ])('%i shows the API\'s words at the top, with the reference', async (status, detail, reference) => {
    const api = fakeApi()
    const user = renderForm(api.action)

    await typeAmountAndSend(user, '150')
    api.answerNext(refusal(status, { detail }, reference))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(detail)
    expect(alert).toHaveTextContent(`Reference: ${reference}`)
  })
})

describe('success', () => {
  it('says who, how much (in rands) and which cycle, and empties the amount for the next payment', async () => {
    const api = fakeApi()
    const user = renderForm(api.action)

    await typeAmountAndSend(user, '150,50')
    api.answerNext(recorded)

    expect(await screen.findByRole('status')).toHaveTextContent('Recorded R 150,50 for you in cycle 2026-11.')
    expect(amountField()).toHaveValue('')
  })
})

describe('what was typed is not lost (WCAG 2.2, 3.3.7 redundant entry)', () => {
  it('after a refusal the amount and the chosen member are still there', async () => {
    const api = fakeApi()
    const user = renderForm(api.action, {
      members: [{ userId: LERATO, name: 'Lerato Mokoena' }, { userId: SIPHO, name: 'Sipho Zulu' }],
    })

    await user.selectOptions(screen.getByLabelText('Payment for'), SIPHO)
    await typeAmountAndSend(user, '1 500,50')
    api.answerNext(refusal(409, { detail: 'Already recorded.' }))

    await screen.findByText(/Already recorded\./)
    expect(amountField()).toHaveValue('1 500,50')
    expect(screen.getByLabelText('Payment for')).toHaveValue(SIPHO)
  })

  it('also after the form\'s own check refuses it', async () => {
    const api = fakeApi()
    const user = renderForm(api.action)

    await typeAmountAndSend(user, '10,555')

    await screen.findByText(/no more than two decimals/)
    expect(amountField()).toHaveValue('10,555')
  })
})

describe('one click, one request', () => {
  it('while the first request is waiting for its answer, a second click sends nothing', async () => {
    const api = fakeApi()
    const user = renderForm(api.action)

    await typeAmountAndSend(user, '150')
    const busy = await screen.findByRole('button', { name: 'Recording…' }) // it changes when the request starts
    expect(busy).toBeDisabled()

    await user.click(busy) // a second click while pending
    await user.click(busy)
    expect(api.sent).toHaveLength(1)

    api.answerNext(recorded)
    await screen.findByRole('status')
    expect(api.sent).toHaveLength(1)
  })
})

describe('the retry key', () => {
  it('a dropped connection and a retry carry the SAME key; the next contribution carries a DIFFERENT one', async () => {
    const api = fakeApi()
    const user = renderForm(api.action)

    // 1. The connection drops: nothing is known about the payment.
    await typeAmountAndSend(user, '150,50')
    api.answerNext(null)
    expect(await screen.findByRole('alert')).toHaveTextContent('This payment may have gone through. Trying again is safe.')
    expect(amountField()).toHaveValue('150,50') // still there, so pressing the button again is the whole retry

    // 2. The person presses the button again: the same payment, the same key. It goes through.
    await user.click(recordButton())
    api.answerNext(recorded)
    await screen.findByRole('status')

    // 3. The NEXT contribution is a different payment: a different key.
    await typeAmountAndSend(user, '75')
    api.answerNext(recorded)
    await screen.findByText(/Recorded R 75,00 for you/)

    expect(api.sent).toHaveLength(3)
    expect(api.sent[1].idempotencyKey).toBe(api.sent[0].idempotencyKey) // same on the retry
    expect(api.sent[2].idempotencyKey).not.toBe(api.sent[0].idempotencyKey) // different for the next contribution
    expect(api.sent.map((s) => s.amountCents)).toEqual([15050, 15050, 7500])
  })

  it('the very first key is the one the server wrote into the page', async () => {
    const api = fakeApi()
    const user = renderForm(api.action)

    await typeAmountAndSend(user, '10')
    api.answerNext(recorded)
    await screen.findByRole('status')

    expect(api.sent[0].idempotencyKey).toBe(FIRST_KEY)
  })
})
