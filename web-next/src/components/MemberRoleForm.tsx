'use client'

import { useActionState } from 'react'
import { toast } from 'sonner'
import { changeMemberRole, type RoleState } from '@/app/stokvelActions'
import { Button } from '@/components/ui/button'
import { ROLE_CHOICES, roleWord } from '@/lib/roles'
import type { MemberRole } from '@/api/types'

/** One member's role picker, for the Chairperson. The API refuses anything it does not allow and the answer is shown here. */
export function MemberRoleForm({
  stokvelId, userId, name, role, lockedByRuleId,
}: {
  stokvelId: string; userId: string; name: string; role: MemberRole
  /** Set for the only Chairperson: the box is disabled and points at the element that explains the rule. */
  lockedByRuleId?: string
}) {
  const [state, formAction, pending] = useActionState(async (previous: RoleState, formData: FormData) => {
    const next = await changeMemberRole(previous, formData)
    if (next.done) toast.success(`${name} is now ${roleWord(String(formData.get('role')) as MemberRole)}.`)
    return next
  }, { message: null, reference: null, done: false } as RoleState)
  const selectId = `role-${userId}`

  return (
    <form action={formAction} className="flex flex-wrap items-center gap-2">
      <input type="hidden" name="stokvelId" value={stokvelId} />
      <input type="hidden" name="userId" value={userId} />
      <label htmlFor={selectId} className="sr-only">Role of {name}</label>
      {/* key = the saved role, so the box shows what the API says after a refresh. */}
      <select id={selectId} name="role" key={role} defaultValue={role}
        disabled={lockedByRuleId !== undefined} aria-describedby={lockedByRuleId}
        className="min-h-10 rounded-[10px] border border-input bg-card px-2.5 text-sm text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:bg-muted disabled:text-muted-foreground">
        {ROLE_CHOICES.map((r) => <option key={r.value} value={r.value}>{r.label}</option>)}
      </select>
      <Button type="submit" variant="secondary" size="sm" className="rounded-[10px] px-3 text-sm" disabled={pending || lockedByRuleId !== undefined}>{pending ? 'Saving…' : 'Save'}</Button>
      {state.message !== null && (
        <p role="alert" className="basis-full text-sm font-medium text-destructive">
          {state.message}{state.reference !== null && <> Reference: <code className="font-mono">{state.reference}</code></>}
        </p>
      )}
    </form>
  )
}
