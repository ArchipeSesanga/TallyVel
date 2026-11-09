import Link from 'next/link'
import { CreateStokvelForm } from '@/components/CreateStokvelForm'

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
