import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { ApiErrorView } from '@/components/ApiErrorView'
import { StokvelCard } from '@/components/StokvelCard'

export default async function StokvelsPage() {
  const home = await readOrFail((init) => api.GET('/api/v1/me/home', init))
  if (!home.ok) return <ApiErrorView {...home.problem} />

  const stokvels = home.data.stokvels
  if (stokvels.length === 0) {
    return (
      <section className="space-y-2">
        <h1 className="text-2xl font-bold">My stokvels</h1>
        <p>You&apos;re not in any stokvel yet. Ask a treasurer to add you.</p>
      </section>
    )
  }

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">My stokvels</h1>
      <ul className="grid gap-4 md:grid-cols-2">
        {stokvels.map((entry) => (
          <li key={entry.stokvelId}>
            {/* The card shows the role from this entry; the link opens the detail page. */}
            <Link href={`/stokvels/${entry.stokvelId}`} className="block">
              <StokvelCard entry={entry} />
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}
