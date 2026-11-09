// The plain facts about a stokvel's fields. NO imports on purpose: the create form in the browser imports this file, and
// the schema (stokvelSchema.ts) imports zod, which is about 32 KB (gzipped) the browser does not need to show a form.
// Found with the bundle analysis: before the split, /stokvels/new shipped all of zod just to read these constants.

// The API's limits (Stokvel.MaxNameLength and MaxRulesLength). The API checks again; this just explains early.
export const NAME_MAX = 100
export const RULES_MAX = 2000

export const FREQUENCIES = ['Weekly', 'BiWeekly', 'Monthly'] as const
export const FREQUENCY_LABELS: Record<(typeof FREQUENCIES)[number], string> = {
  Weekly: 'Weekly',
  BiWeekly: 'Every two weeks',
  Monthly: 'Monthly',
}
