import { z } from 'zod'
import { parseRandsToCents } from './moneyInput'

/**
 * What the contribution form sends. `amount` is the TEXT the person typed ("150.50");
 * parseRandsToCents turns it into whole cents with string logic only, never a fractional number.
 */
export const contributionSchema = z.object({
  stokvelId: z.uuid('This stokvel could not be identified. Reload the page.'),
  memberUserId: z.uuid('Choose who this payment is for.'),
  cycle: z.string().trim().min(1, 'The cycle is missing. Reload the page.'),
  amount: z
    .string()
    .trim()
    .min(1, 'Enter an amount.')
    .refine((text) => parseRandsToCents(text) !== null, 'Enter an amount in rands, like 150 or 150.50 (cents only, no more than two decimals).')
    .refine((text) => {
      const cents = parseRandsToCents(text)
      return cents === null || cents > 0 // not a number is the check above; this one is only about zero
    }, 'The amount must be more than R 0,00.'),
  idempotencyKey: z.uuid('The form is out of date. Reload the page.'),
})

export type ContributionInput = z.infer<typeof contributionSchema>
