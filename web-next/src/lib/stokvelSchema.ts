import { z } from 'zod'
import { parseRandsToCents } from './moneyInput'

// The API's limits (Stokvel.MaxNameLength and MaxRulesLength). The API checks again; this just explains early.
export const NAME_MAX = 100
export const RULES_MAX = 2000

export const FREQUENCIES = ['Weekly', 'BiWeekly', 'Monthly'] as const
export const FREQUENCY_LABELS: Record<(typeof FREQUENCIES)[number], string> = {
  Weekly: 'Weekly',
  BiWeekly: 'Every two weeks',
  Monthly: 'Monthly',
}

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
