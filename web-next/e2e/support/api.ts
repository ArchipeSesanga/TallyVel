import fs from 'node:fs'
import type { APIRequestContext } from '@playwright/test'
import { API, PEOPLE, type Person } from './env'

// Preparing data for a test, through the API, as one of the demo people. (Each test makes its OWN stokvel with a
// unique name, so no test depends on another test or on what an earlier run left behind.)

function tokenOf(person: Person): string {
  return (JSON.parse(fs.readFileSync(PEOPLE[person].tokenFile, 'utf8')) as { accessToken: string }).accessToken
}

/** The current South African month as a cycle label, "2026-11". */
export function currentCycleLabel(now: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Johannesburg', year: 'numeric', month: '2-digit' }).format(now)
}

/** A name nobody else will have: the test's own stokvel. */
export function uniqueName(prefix: string, workerIndex: number): string {
  return `${prefix} ${Date.now().toString(36)}-${workerIndex}`
}

/** Creates a stokvel as `person` (who becomes its Chairperson) and opens this month's cycle. Returns its id and name. */
export async function createStokvelWithOpenCycle(
  request: APIRequestContext, person: Person, name: string, contributionAmountCents: number,
): Promise<{ id: string; name: string }> {
  const headers = { Authorization: `Bearer ${tokenOf(person)}` }

  const me = await request.get(`${API}/api/v1/me`, { headers })
  if (!me.ok()) throw new Error(`GET /me failed: ${me.status()}`)
  const { id: creatorId } = (await me.json()) as { id: string }

  const created = await request.post(`${API}/api/v1/stokvels`, {
    headers, data: { name, contributionAmountCents, cycle: 'Monthly', creatorId },
  })
  if (!created.ok()) throw new Error(`create stokvel failed: ${created.status()} ${await created.text()}`)
  const { id } = (await created.json()) as { id: string }

  const cycle = await request.post(`${API}/api/v1/stokvels/${id}/cycles`, { headers, data: { label: currentCycleLabel() } })
  if (!cycle.ok()) throw new Error(`open cycle failed: ${cycle.status()} ${await cycle.text()}`)

  return { id, name }
}

/** The id of one of the person's own stokvels, by name (read from their own /me/home). */
export async function stokvelIdByName(request: APIRequestContext, person: Person, name: string): Promise<string> {
  const home = await request.get(`${API}/api/v1/me/home`, { headers: { Authorization: `Bearer ${tokenOf(person)}` } })
  const { stokvels } = (await home.json()) as { stokvels: { stokvelId: string; name: string }[] }
  const found = stokvels.find((s) => s.name === name)
  if (found === undefined) throw new Error(`${person} is not in "${name}": is the demo seed loaded?`)
  return found.stokvelId
}

/** A member of that stokvel, by (part of) their name, as the person sees the member list. */
export async function memberIdByName(request: APIRequestContext, person: Person, stokvelId: string, namePart: string): Promise<string> {
  const detail = await request.get(`${API}/api/v1/stokvels/${stokvelId}`, { headers: { Authorization: `Bearer ${tokenOf(person)}` } })
  const { members } = (await detail.json()) as { members: { userId: string; fullName?: string | null }[] }
  const found = members.find((m) => m.fullName?.includes(namePart))
  if (found === undefined) throw new Error(`no member called "${namePart}" in that stokvel`)
  return found.userId
}

/** The id of a demo person (read with their own token). */
export async function userIdOf(request: APIRequestContext, person: Person): Promise<string> {
  const me = await request.get(`${API}/api/v1/me`, { headers: { Authorization: `Bearer ${tokenOf(person)}` } })
  return ((await me.json()) as { id: string }).id
}

/** `owner` (a Chairperson) adds `person` to the stokvel as an ordinary Member. */
export async function addAsMember(request: APIRequestContext, owner: Person, stokvelId: string, person: Person): Promise<void> {
  const added = await request.post(`${API}/api/v1/stokvels/${stokvelId}/members`, {
    headers: { Authorization: `Bearer ${tokenOf(owner)}` },
    data: { userId: await userIdOf(request, person), role: 'Member' },
  })
  if (!added.ok()) throw new Error(`add member failed: ${added.status()} ${await added.text()}`)
}

/** `owner` (Treasurer or Chairperson) makes an invite code for the stokvel. */
export async function createInviteCode(request: APIRequestContext, owner: Person, stokvelId: string): Promise<string> {
  const response = await request.post(`${API}/api/v1/stokvels/${stokvelId}/invites`, { headers: { Authorization: `Bearer ${tokenOf(owner)}` } })
  if (!response.ok()) throw new Error(`create invite failed: ${response.status()} ${await response.text()}`)
  return ((await response.json()) as { code: string }).code
}
