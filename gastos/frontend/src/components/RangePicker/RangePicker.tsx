import type { MonthRange, RangePreset } from '../../shared/evolutionRange.ts'
import MonthPicker from '../MonthPicker/MonthPicker.tsx'

interface Props {
  /** Which shortcut is on, or 'custom' when the range was written by hand (then none is pressed). */
  mode: RangePreset | 'custom'
  /** What the two fields show, yyyy-MM: the range in use, or what the user wrote when it cannot be asked for. */
  from: string
  to: string
  onPreset: (preset: RangePreset) => void
  onCustom: (range: MonthRange) => void
}

const PRESETS: readonly { value: RangePreset; label: string }[] = [
  { value: '3', label: 'Últimos 3 meses' },
  { value: '6', label: 'Últimos 6 meses' },
  { value: '12', label: 'Últimos 12 meses' },
  { value: 'anio', label: 'Este año' },
]

/**
 * Where the evolution starts and ends: shortcuts for the common cases and two month fields for any other range.
 * It only reports what the user chose; what the range means (and whether it is valid) is decided by the view.
 */
const RangePicker = ({ mode, from, to, onPreset, onCustom }: Props) => {
  const handleChoosePreset = (preset: RangePreset) => () => onPreset(preset)

  const handleChangeFrom = (next: string) => onCustom({ from: next, to })

  const handleChangeTo = (next: string) => onCustom({ from, to: next })

  return (
    <div className="flex flex-col gap-3">
      <div role="group" aria-label="Rango de meses" className="flex flex-wrap gap-2">
        {PRESETS.map((preset) => (
          <button
            key={preset.value}
            type="button"
            aria-pressed={mode === preset.value}
            onClick={handleChoosePreset(preset.value)}
            className={`cursor-pointer rounded-sm border-2 border-ink px-2.5 py-1 pointer-coarse:min-h-11 text-xs font-bold focus-visible:outline-3 focus-visible:outline-pen ${
              mode === preset.value ? 'bg-mark text-on-mark' : 'text-ink hover:bg-mark hover:text-on-mark'
            }`}
          >
            {preset.label}
          </button>
        ))}
      </div>

      <div className="flex flex-wrap gap-3">
        {/* A month cannot be cleared in this picker, so a range always has its two ends */}
        <MonthPicker label="Desde" value={from} onChange={handleChangeFrom} />
        <MonthPicker label="Hasta" value={to} onChange={handleChangeTo} />
      </div>
    </div>
  )
}

export default RangePicker
