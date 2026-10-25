import { Link } from 'react-router-dom'
import { useHome, useMe } from '../api/queries'

/** The landing page: a short hello and a way into the two working pages. */
export default function Dashboard() {
  const me = useMe()
  const home = useHome()

  return (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold">
        {me.data ? `Welcome, ${me.data.fullName}` : 'Welcome'}
      </h2>
      {home.isSuccess && (
        <p className="text-sm text-muted-foreground">
          You are in {home.data.length} {home.data.length === 1 ? 'stokvel' : 'stokvels'}.
        </p>
      )}
      <div className="flex flex-wrap gap-3 text-sm font-medium">
        <Link to="/stokvels" className="rounded-md border border-border px-3 py-2 hover:bg-muted">
          My stokvels
        </Link>
        <Link to="/contributions" className="rounded-md border border-border px-3 py-2 hover:bg-muted">
          Record or view contributions
        </Link>
      </div>
    </div>
  )
}
