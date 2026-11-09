import type { Client } from 'openapi-fetch'
import type { paths } from '@/api/schema'
import type { ApiAnswer, RecordPort } from './recordContribution'

/** The API's correlation id from a problem+json body, if it has one. (Also in lib/api.ts, which is server-only.) */
function correlationIdIn(body: unknown): string | null {
  if (typeof body === 'object' && body !== null && 'correlationId' in body && typeof body.correlationId === 'string') return body.correlationId
  return null
}

/**
 * The real "send" of a RecordPort: POST the contribution with the person's token and the Idempotency-Key.
 * The client is passed in (the Server Action gives it the app's client; a test gives it one aimed at a fake API).
 */
export function sendWithClient(client: Client<paths>, accessToken: string): RecordPort['send'] {
  return async ({ stokvelId, memberUserId, cycle, amountCents, idempotencyKey }): Promise<ApiAnswer | null> => {
    const result = await client
      .POST('/api/v1/stokvels/{stokvelId}/contributions', {
        headers: { Authorization: `Bearer ${accessToken}` },
        params: { path: { stokvelId }, header: { 'Idempotency-Key': idempotencyKey } },
        body: { memberUserId, cycle, amountCents },
      })
      .catch(() => null) // no answer at all: the connection dropped
    if (result === null) return null
    return {
      status: result.response.status,
      recorded: result.data !== undefined,
      problem: result.error,
      correlationId: correlationIdIn(result.error),
      retryAfter: result.response.headers.get('Retry-After'),
    }
  }
}
