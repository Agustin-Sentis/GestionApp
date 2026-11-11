import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId } from 'react'
import { api } from '../api.ts'
import { useToast } from './toastContext.ts'

/**
 * The switch for the budget alert mails, at the foot of the budgets card. It is off until the user turns it on. Turning it on
 * with an address that is not confirmed yet sends a mail with a link; the alerts start once that link is opened, and the
 * text under the switch says which of the three states the user is in. The state is always written, never only shown.
 */
export default function BudgetAlertsToggle() {
  const queryClient = useQueryClient()
  const toast = useToast()
  const statusId = useId()

  // The same query as the dollar control: one request, one cache
  const { data: settings } = useQuery({ queryKey: ['settings'], queryFn: api.settings.get })

  const change = useMutation({
    mutationFn: (enabled: boolean) => api.settings.setBudgetAlerts(enabled),
    onSuccess: (saved, enabled) => {
      queryClient.setQueryData(['settings'], saved)
      toast.success(
        !enabled
          ? 'Avisos por correo desactivados'
          : saved.emailConfirmed
            ? 'Avisos por correo activados'
            : 'Te mandamos un correo para confirmar',
      )
    },
    // The server says why: the mail could not be sent, or one was sent a moment ago. The switch goes back to what is saved.
    onError: (error) => toast.error(error.message),
  })

  // Without the saved state there is nothing to switch (still loading, or the request failed)
  if (!settings) return null

  // While it is being saved, show what the user just chose instead of snapping back
  const checked = change.isPending ? change.variables : settings.budgetEmailAlerts

  return (
    <div className="flex flex-col gap-1.5 border-t border-line pt-3">
      <label className="flex cursor-pointer items-start gap-2.5 text-sm font-semibold">
        <input
          type="checkbox"
          className="mt-0.5 size-4 shrink-0"
          checked={checked}
          disabled={change.isPending}
          aria-describedby={statusId}
          onChange={(e) => change.mutate(e.target.checked)}
        />
        <span>Avisarme por correo cuando un presupuesto llegue al 80% o lo supere</span>
      </label>

      <p id={statusId} className="muted text-sm" role="status">
        {!settings.budgetEmailAlerts ? (
          'Apagado. No te mandamos ningún correo.'
        ) : settings.emailConfirmed ? (
          <>Activados. Te avisamos a <strong className="text-ink">{settings.email}</strong>.</>
        ) : (
          <>
            Te mandamos un correo a <strong className="text-ink">{settings.email}</strong> para confirmar. Los avisos empiezan
            cuando hagas clic en el enlace.{' '}
            <button type="button" className="btn-link" disabled={change.isPending} onClick={() => change.mutate(true)}>
              Reenviar el correo
            </button>
          </>
        )}
      </p>
    </div>
  )
}
