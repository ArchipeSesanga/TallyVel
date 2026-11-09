// What the BROWSER checks about a proof before sending it. This is only a courtesy so a wrong file is explained
// at once, under the input. The API checks again, by the file's bytes, and has the last word.

/** The API's limit: 5 MB, counted in bytes the way the API counts them. */
export const MAX_PROOF_BYTES = 5 * 1024 * 1024

/** For the file input's accept attribute (a hint for the file picker, not a check). */
export const PROOF_ACCEPT = 'image/jpeg,image/png,application/pdf'

export type ProofType = 'jpeg' | 'png' | 'pdf'

/** The type of a file from its first bytes (its "magic number"), never from its name or the browser's Content-Type. */
export function detectProofType(head: Uint8Array): ProofType | null {
  const starts = (...bytes: number[]) => bytes.every((b, i) => head[i] === b)
  if (starts(0xff, 0xd8, 0xff)) return 'jpeg'
  if (starts(0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a)) return 'png'
  if (starts(0x25, 0x50, 0x44, 0x46, 0x2d)) return 'pdf' // "%PDF-"
  return null
}

export const FILE_TOO_BIG = 'This file is bigger than 5 MB. Choose a smaller photo or PDF.'
export const FILE_WRONG_TYPE = "This isn't a JPEG, PNG or PDF. Choose a photo or a PDF of your proof of payment."
export const FILE_EMPTY = 'This file is empty. Choose another one.'

/** The message to show under the input, or null if the file looks fine. `head` is the file's first bytes. */
export function checkProofFile(size: number, head: Uint8Array): string | null {
  if (size === 0) return FILE_EMPTY
  if (size > MAX_PROOF_BYTES) return FILE_TOO_BIG
  if (detectProofType(head) === null) return FILE_WRONG_TYPE
  return null
}

/** Reads only the first bytes of a File (not the whole file) and checks it. */
export async function checkProofFileInBrowser(file: File): Promise<string | null> {
  const head = new Uint8Array(await file.slice(0, 16).arrayBuffer())
  return checkProofFile(file.size, head)
}
