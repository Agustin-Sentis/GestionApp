/** What the summary panel shows: the month on screen, or how the months have evolved. */
export type SummaryMode = 'mes' | 'evolucion'

interface Props {
  mode: SummaryMode
  onChange: (mode: SummaryMode) => void
}

const OPTIONS: readonly { value: SummaryMode; label: string }[] = [
  { value: 'mes', label: 'Este mes' },
  { value: 'evolucion', label: 'Evolución' },
]

/** Two buttons side by side, like the theme and dollar controls: the one that is on is highlighted and `aria-pressed`. */
const SummaryModeToggle = ({ mode, onChange }: Props) => {
  const handleChooseMode = (value: SummaryMode) => () => onChange(value)

  return (
    <div role="group" aria-label="Vista del resumen" className="inline-flex divide-x-2 divide-ink self-start rounded-sm border-2 border-ink">
      {OPTIONS.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={mode === option.value}
          onClick={handleChooseMode(option.value)}
          className={`h-9 cursor-pointer px-3 pointer-coarse:h-11 text-xs font-bold focus-visible:outline-3 focus-visible:-outline-offset-3 focus-visible:outline-pen ${
            mode === option.value ? 'bg-mark text-on-mark' : 'text-ink-soft hover:text-ink'
          }`}
        >
          {option.label}
        </button>
      ))}
    </div>
  )
}

export default SummaryModeToggle
