import Link from 'next/link'

// One neutral sentence for every case (malformed id, missing stokvel, not your stokvel),
// so this page can't be used to find out whether a stokvel exists.
export default function StokvelNotFound() {
  return (
    <section className="space-y-2">
      <h1 className="text-2xl font-bold">We couldn&apos;t find that stokvel</h1>
      <p>
        <Link href="/stokvels" className="underline">
          Back to my stokvels
        </Link>
      </p>
    </section>
  )
}
