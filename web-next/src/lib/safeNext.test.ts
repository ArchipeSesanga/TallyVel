import { describe, expect, it } from 'vitest'
import { safeNextPath } from './safeNext'

describe('safeNextPath', () => {
  it('follows a path on our own site', () => {
    expect(safeNextPath('/stokvels/b12b7944-7790-4aa6-a450-535434d34b05')).toBe('/stokvels/b12b7944-7790-4aa6-a450-535434d34b05')
  })

  it('keeps a query string', () => {
    expect(safeNextPath('/stokvels?tab=members')).toBe('/stokvels?tab=members')
  })

  it('keeps the whole query string of a join link (the same step and code come back after sign-in)', () => {
    expect(safeNextPath('/join?code=-7R8qvcwyFW3lqWL8Cc2Ow&step=rules')).toBe('/join?code=-7R8qvcwyFW3lqWL8Cc2Ow&step=rules')
  })

  it('keeps a query string that is encoded inside ?next= and refuses one that points elsewhere', () => {
    const next = new URLSearchParams(new URLSearchParams({ next: '/join?code=AbC_-123&step=confirm' }).toString()).get('next')
    expect(safeNextPath(next)).toBe('/join?code=AbC_-123&step=confirm')
    expect(safeNextPath('//evil.example/join?code=AbC_-123&step=confirm')).toBe('/dashboard/member')
  })

  it('refuses //evil.example (another site)', () => {
    expect(safeNextPath('//evil.example')).toBe('/dashboard/member')
  })

  it('refuses /\\evil.example (browsers treat \\ as /)', () => {
    expect(safeNextPath('/\\evil.example')).toBe('/dashboard/member')
  })

  it('refuses a full URL to another site', () => {
    expect(safeNextPath('https://evil.example/login')).toBe('/dashboard/member')
  })

  it('refuses a tab hidden inside the slashes', () => {
    expect(safeNextPath('/\t/evil.example')).toBe('/dashboard/member')
  })

  it('uses the dashboard when there is no next', () => {
    expect(safeNextPath(null)).toBe('/dashboard/member')
    expect(safeNextPath('')).toBe('/dashboard/member')
  })
})