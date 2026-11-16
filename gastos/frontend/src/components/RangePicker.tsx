import type { MonthRange, RangePreset } from '../evolutionRange.ts'
import { MAX_MONTH, MIN_MONTH } from '../format.ts'

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
export default function RangePicker({ mode, from, to, onPreset, onCustom }: Props) {
  return (
    <div className="flex flex-col gap-3">
      <div role="group" aria-label="Rango de meses" className="flex flex-wrap gap-2">
        {PRESETS.map((preset) => (
          <button
            key={preset.value}
            type="button"
            aria-pressed={mode === preset.value}
            onClick={() => onPreset(preset.value)}
            className={`cursor-pointer rounded-sm border-2 border-ink px-2.5 py-1 text-xs font-bold focus-visible:outline-3 focus-visible:outline-pen ${
              mode === preset.value ? 'bg-mark text-on-mark' : 'text-ink hover:bg-mark hover:text-on-mark'
            }`}
          >
            {preset.label}
          </button>
        ))}
      </div>

      <div className="flex flex-wrap gap-3">
        <label className="field">
          Desde
          <input
            type="month"
            className="input"
            value={from}
            min={MIN_MONTH}
            max={MAX_MONTH}
            // A cleared field is not a range: it is ignored, and the field keeps showing the range in use
            onChange={(e) => e.target.value && onCustom({ from: e.target.value, to })}
          />
        </label>
        <label className="field">
          Hasta
          <input
            type="month"
            className="input"
            value={to}
            min={MIN_MONTH}
            max={MAX_MONTH}
            onChange={(e) => e.target.value && onCustom({ from, to: e.target.value })}
          />
        </label>
      </div>
    </div>
  )
}
