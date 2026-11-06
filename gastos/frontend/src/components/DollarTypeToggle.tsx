import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId } from 'react'
import { api, DOLLAR_TYPES } from '../api.ts'
import type { DollarType } from '../api.ts'
import { DOLLAR_HINTS, DOLLAR_LABELS, DOLLAR_SCOPE } from '../dollar.ts'
import { useToast } from './toastContext.ts'

/**
 * Lets the user pick which dollar the USD movements they load FROM NOW ON are saved with: official, blue or MEP.
 * Each movement keeps the dollar it was saved with, so choosing another one changes nothing that is already saved:
 * that is why nothing is asked for again here, and why every note says so.
 * Like the theme control, the three choices are always in view as radio buttons, and hovering or focusing one explains it.
 */
export default function DollarTypeToggle() {
  const queryClient = useQueryClient()
  const toast = useToast()
  const idPrefix = useId()

  const { data: settings } = useQuery({ queryKey: ['settings'], queryFn: api.settings.get })

  const change = useMutation({
    mutationFn: (type: DollarType) => api.settings.update(type),
    onSuccess: (saved) => {
      // Only the saved choice changes. The list, the figures and the charts are untouched: nothing already saved is different.
      queryClient.setQueryData(['settings'], saved)
      toast.success(`Los movimientos nuevos se guardan con el dólar ${DOLLAR_LABELS[saved.dollarType]}`)
    },
    onError: (error) => toast.error(error.message),
  })

  // While the change is being saved, show what the user just chose instead of snapping back to the old one
  const current = change.isPending ? change.variables : settings?.dollarType
  // Without the saved choice there is nothing to change yet (or the request failed): the buttons stay, but do nothing
  const disabled = !settings || change.isPending

  return (
    <div className="inline-flex items-center gap-2">
      <span aria-hidden="true" className="muted text-[0.7rem] font-semibold tracking-wide uppercase">Dólar</span>

      {/* No overflow clipping here: the note hangs below the control and must not be cut off */}
      <fieldset className="relative inline-flex divide-x-2 divide-ink rounded-sm border-2 border-ink">
        <legend className="sr-only">Dólar con el que se guardan los movimientos nuevos</legend>
        {DOLLAR_TYPES.map((value) => {
          const hintId = `${idPrefix}-${value}`
          return (
            <div key={value} className="group">
              <label className="grid h-9 min-w-14 cursor-pointer place-items-center px-2.5 text-xs font-bold text-ink-soft hover:text-ink has-checked:bg-mark has-checked:text-on-mark has-disabled:cursor-wait has-focus-visible:outline-3 has-focus-visible:-outline-offset-3 has-focus-visible:outline-pen">
                <input
                  type="radio"
                  name="dollar-type"
                  value={value}
                  checked={current === value}
                  disabled={disabled}
                  aria-describedby={hintId}
                  onChange={() => change.mutate(value)}
                  className="sr-only"
                />
                {DOLLAR_LABELS[value]}
              </label>

              {/* Outside the label, so it is the description of the choice and not part of its name */}
              <span
                id={hintId}
                role="tooltip"
                className="pointer-events-none invisible absolute top-full right-0 z-30 mt-2 w-max max-w-52 rounded-sm border-2 border-ink bg-sheet px-2.5 py-1.5 text-left text-xs leading-snug font-medium text-ink opacity-0 transition-opacity delay-100 group-hover:visible group-hover:opacity-100 group-has-focus-visible:visible group-has-focus-visible:opacity-100 motion-reduce:transition-none"
              >
                <strong className="display block text-[0.7rem] uppercase" style={{ letterSpacing: '0.05em' }}>
                  {DOLLAR_LABELS[value]}
                </strong>
                {DOLLAR_HINTS[value]} {DOLLAR_SCOPE}
              </span>
            </div>
          )
        })}
      </fieldset>
    </div>
  )
}
