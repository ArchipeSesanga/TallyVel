import type { Metadata } from 'next'
import Link from 'next/link'
import { CreateStokvelForm } from '@/components/CreateStokvelForm'

// The browser tab and a screen reader's page title. Each page names itself; the site name is added after it.
export const metadata: Metadata = { title: 'Create a stokvel' }

// Static: nothing here reads the request. The form posts to a Server Action that reads the session.
export default function NewStokvelPage() {
  return (
    <section className="space-y-4">
      <p className="text-sm"><Link href="/stokvels" className="underline">← My stokvels</Link></p>
      <h1 className="text-2xl font-bold">Create a stokvel</h1>
      <CreateStokvelForm />
    </section>
  )
}
