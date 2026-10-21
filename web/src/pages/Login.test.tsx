import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// Login reads the API address when session.ts loads, so set it before importing.
vi.stubEnv('VITE_API_BASE_URL', 'http://api.test')
const { default: Login } = await import('./Login')

function respond(status: number, body: unknown = {}, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  })
}

function signIn() {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'lerato@tallyvel.test' } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'whatever-it-is' } })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('Login feedback', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the email or password is incorrect on a 401, without saying which', async () => {
    vi.mocked(fetch).mockResolvedValue(respond(401, { status: 401 }))
    render(<Login onSignedIn={() => {}} />)

    signIn()

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.')
  })

  it('gives the wait time from Retry-After on a 429', async () => {
    vi.mocked(fetch).mockResolvedValue(respond(429, { status: 429 }, { 'Retry-After': '23' }))
    render(<Login onSignedIn={() => {}} />)

    signIn()

    expect(await screen.findByRole('alert')).toHaveTextContent('Try again in 23 seconds.')
  })

  it('says it cannot reach the API when there is no response at all', async () => {
    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))
    render(<Login onSignedIn={() => {}} />)

    signIn()

    expect(await screen.findByRole('alert')).toHaveTextContent("Can't reach RondiTrack")
  })

  it('shows "Signing in…" while it waits, and disables the button', async () => {
    vi.mocked(fetch).mockReturnValue(new Promise(() => {})) // never answers
    render(<Login onSignedIn={() => {}} />)

    signIn()

    const button = await screen.findByRole('button', { name: 'Signing in…' })
    expect(button).toBeDisabled()
  })

  it('calls onSignedIn after a successful sign-in', async () => {
    vi.mocked(fetch).mockResolvedValue(respond(200, { accessToken: 'abc' }))
    const onSignedIn = vi.fn()
    render(<Login onSignedIn={onSignedIn} />)

    signIn()

    await vi.waitFor(() => expect(onSignedIn).toHaveBeenCalledTimes(1))
  })
})
