import { describe, expect, it } from 'vitest'
import { FILE_EMPTY, FILE_TOO_BIG, FILE_WRONG_TYPE, MAX_PROOF_BYTES, checkProofFile, detectProofType } from './proofFile'

const PNG = Uint8Array.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0])
const JPEG = Uint8Array.from([0xff, 0xd8, 0xff, 0xe0, 0, 0x10])
const PDF = new TextEncoder().encode('%PDF-1.7\n')
const TEXT = new TextEncoder().encode('hello, I am a text file')

describe('detectProofType', () => {
  it('reads the type from the first bytes', () => {
    expect(detectProofType(PNG)).toBe('png')
    expect(detectProofType(JPEG)).toBe('jpeg')
    expect(detectProofType(PDF)).toBe('pdf')
  })

  it('does not accept text, even text that mentions pdf', () => {
    expect(detectProofType(TEXT)).toBeNull()
    expect(detectProofType(new TextEncoder().encode('PDF %PDF-'))).toBeNull()
    expect(detectProofType(new Uint8Array())).toBeNull()
  })
})

describe('checkProofFile', () => {
  it('accepts a file just under the limit, exactly at it, and a small one', () => {
    expect(checkProofFile(MAX_PROOF_BYTES - 1, PNG)).toBeNull()
    expect(checkProofFile(MAX_PROOF_BYTES, PNG)).toBeNull()
    expect(checkProofFile(120_000, PDF)).toBeNull()
  })

  it('refuses one byte over 5 MB, like the API', () => {
    expect(checkProofFile(MAX_PROOF_BYTES + 1, PNG)).toBe(FILE_TOO_BIG)
    expect(checkProofFile(6 * 1024 * 1024, PNG)).toBe(FILE_TOO_BIG)
  })

  it('refuses a text file whatever it is called (a name is never looked at)', () => {
    expect(checkProofFile(23, TEXT)).toBe(FILE_WRONG_TYPE)
  })

  it('refuses an empty file', () => {
    expect(checkProofFile(0, new Uint8Array())).toBe(FILE_EMPTY)
  })

  it('the limit is exactly 5 * 1024 * 1024', () => {
    expect(MAX_PROOF_BYTES).toBe(5_242_880)
  })
})
