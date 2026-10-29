import type { MeHomeEntry } from '@/api/types'

/**
 * The stokvels where this person is the Treasurer. The role is read from each /me/home
 * entry (the API sends one role per stokvel), so a person can be Treasurer in one
 * stokvel and a plain Member in another.
 */
export function stokvelsWhereTreasurer(entries: MeHomeEntry[]): MeHomeEntry[] {
  return entries.filter((entry) => entry.role === 'Treasurer')
}
