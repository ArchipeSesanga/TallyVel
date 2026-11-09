const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** True for the shape of an id (the API checks that it really exists and is ours). */
export function isUuid(value: string): boolean {
  return UUID.test(value)
}
