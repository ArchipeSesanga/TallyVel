import { z } from 'zod'
import { parseRandsToCents } from './moneyInput'

import { FREQUENCIES, NAME_MAX, RULES_MAX } from './stokvelFields'

// Re-exported so the tests (and the server) keep one place to import from. The browser form imports stokvelFields instead.
export { FREQUENCIES, FREQUENCY_LABELS, NAME_MAX, RULES_MAX } from './stokvelFields'

export const createStokvelSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name for the stokvel.').max(NAME_MAX, `Use ${NAME_MAX} characters or fewer.`),
  // Text in, whole cents out (see moneyInput.ts): never a fractional number.
  amount: z.string().refine((v) => {
    const cents = parseRandsToCents(v)
    return cents !== null && cents > 0
  }, 'Enter an amount in rands, like 500 or 500,50.'),
  frequency: z.enum(FREQUENCIES, 'Choose how often members pay.'),
  rules: z.string().trim().max(RULES_MAX, `Use ${RULES_MAX} characters or fewer.`),
})
