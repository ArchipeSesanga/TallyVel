import { readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, expect, it } from 'vitest'

// A guard, not a behaviour test: the files that handle money must not contain the usual ways of losing a cent.
// (Seconds, days and the like in other files are not money, so only these files are looked at.)
const MONEY_FILES = ['lib/money.ts', 'lib/moneyInput.ts', 'lib/contributionSchema.ts']
const BANNED = [/parseFloat/, /\.toFixed\(/, /Math\.round/, /Math\.floor/, /Math\.ceil/, /\*\s*100\b/, /\/\s*100\b/, /\bNumber\([^)]*\)\s*\*/]

describe('money code never uses floating-point shortcuts', () => {
  it.each(MONEY_FILES)('%s', (file) => {
    // Comments may talk about these things ("no Math.round"); only real code counts.
    const code = readFileSync(path.join(__dirname, '..', file), 'utf8')
      .split('\n')
      .filter((line) => !line.trim().startsWith('//') && !line.trim().startsWith('*') && !line.trim().startsWith('/*'))
      .map((line) => line.replace(/\/\/.*$/, ''))
      .join('\n')
    for (const pattern of BANNED) expect(code).not.toMatch(pattern)
  })
})
