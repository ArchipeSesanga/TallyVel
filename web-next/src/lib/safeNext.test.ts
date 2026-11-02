import { describe, expect, it } from 'vitest'
import { safeNextPath } from './safeNext'

describe('safeNextPath', () => {
  it('follows a path on our own site', () => {
    expect(safeNextPath('/stokvels/b12b7944-7790-4aa6-a450-535434d34b05')).toBe('/stokvels/b12b7944-7790-4aa6-a450-535434d34b05')
  })

  it('keeps a query string', () => {
    expect(safeNextPath('/stokvels?tab=members')).toBe('/stokvels?tab=members')
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