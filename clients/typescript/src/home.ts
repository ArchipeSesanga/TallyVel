import type { components } from "./api";

export type HomeResponse = components["schemas"]["HomeResponse"];
export type HomeStokvelCard = components["schemas"]["HomeStokvelCard"];

/** Total the caller still owes this cycle, across all their stokvels, in cents. */
export function totalOwedCents(home: HomeResponse): number {
  return home.stokvels.reduce((sum, card) => sum + card.owedThisCycleCents, 0);
}

/** Integer cents to a display string, e.g. 39950 -> "R399.50". */
export function formatRand(cents: number): string {
  return `R${(cents / 100).toFixed(2)}`;
}
