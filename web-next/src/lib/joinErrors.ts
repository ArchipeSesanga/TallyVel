import { tryAgainIn } from './errorMessages'

/** Why checking an invite code failed. Each kind has one message for the person (see joinErrorMessage). */
export type JoinErrorKind = 'not-found' | 'expired' | 'already-member' | 'rate-limited' | 'unreachable' | 'failed'

export class JoinError extends Error {
  constructor(
    readonly kind: JoinErrorKind,
    /** Retry-After for 'rate-limited', or the API's reference for 'failed'. */
    readonly detail: string | null = null,
  ) {
    super(kind)
  }
}

export function joinErrorMessage(error: JoinError): string {
  switch (error.kind) {
    case 'not-found':
      return "That code doesn't match any stokvel. Check it and try again."
    case 'expired':
      return 'This invite has expired. Ask the treasurer for a new one.'
    case 'already-member':
      return "You're already in this stokvel."
    case 'rate-limited':
      return tryAgainIn(error.detail)
    case 'unreachable':
      return "Couldn't reach TallyVel. Check your connection and try again."
    case 'failed':
      return `Something went wrong. Please try again.${error.detail !== null ? ` Reference: ${error.detail}` : ''}`
  }
}
