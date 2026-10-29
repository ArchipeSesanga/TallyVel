'use client' // error boundaries must be Client Components: they keep the "something broke" state

export default function AppError({ error, retry }: { error: unknown; retry: () => void }) {
  // Only the digest (a short id that also appears in the server log) is shown. Never a stack
  // trace or error.message: in production Next.js hides them, and they can leak internals.
  const digest =
    typeof error === 'object' && error !== null && 'digest' in error && typeof error.digest === 'string'
      ? error.digest
      : null

  return (
    <div role="alert" className="space-y-3 rounded-lg border border-border p-4">
      <p>Something went wrong. Please try again.</p>
      {digest !== null && (
        <p className="text-sm text-muted-foreground">
          Reference: <code className="select-all font-mono">{digest}</code>
        </p>
      )}
      <button type="button" onClick={() => retry()} className="rounded-md border border-border px-3 py-2 text-sm">
        Try again
      </button>
    </div>
  )
}
