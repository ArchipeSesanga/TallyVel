import { redirect } from 'next/navigation'

// The app has no home page of its own: signed-in people land on the member dashboard,
// and the dashboard's layout sends anyone without a session to /login.
export default function Home() {
  redirect('/dashboard/member')
}
