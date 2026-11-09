/**
 * Where each kind of answer is shown (README, "Feedback"):
 * - fileError: the file itself is wrong (413, 415, no file). Always UNDER the input, never a toast.
 * - message:   a refusal about the situation (locked, not allowed, wait). Inline, next to the form.
 * - unexpected: a 5xx or no answer. The browser shows a toast that stays, with the reference.
 */
export type ProofState = {
  status: 'idle' | 'done' | 'removed'
  fileError: string | null
  message: string | null
  unexpected: { message: string; reference: string } | null
}

// Lives here, not in proofActions.ts: a "use server" file may export only async functions.
export const PROOF_IDLE: ProofState = { status: 'idle', fileError: null, message: null, unexpected: null }
