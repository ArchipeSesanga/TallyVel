import 'server-only'
import createClient from 'openapi-fetch'
import { notFound, redirect } from 'next/navigation'
import type { paths } from '@/api/schema'
import { getSession } from './session'
import { describeApiError } from './errorMessages' 

export const api = createClient<paths>({ baseUrl: process.env.API_BASE_URL })

export type ApiProblem = { message: string; reference: string | null }
export type ReadResult<T> = { ok: true; data: T } | { ok: false; problem: ApiProblem }

type AuthInit = { headers: { Authorization: string } }
type Call<T> = (init: AuthInit) => Promise<{ data?: T; error?: unknown; response: Response }>

export async function readOrFail<T>(call: Call<T>): Promise<ReadResult<T>> {
  const session = await getSession()
  if (session === null) redirect('/login')

  const { data, error, response } = await call({
    headers: { Authorization: `Bearer ${session.accessToken}` },
  })

  if (error === undefined && data !== undefined) return { ok: true, data }
  if (response.status === 401) redirect('/login?expired=1')
  if (response.status === 404) notFound()

  return {
    ok: false,
    problem: {
      message: describeApiError(response.status, response.headers.get('Retry-After')),
      reference: correlationIdOf(error),
    },
  }
}

export function correlationIdOf(body: unknown): string | null {
  if (typeof body === 'object' && body !== null && 'correlationId' in body && typeof body.correlationId === 'string') {
    return body.correlationId
  }
  return null
}