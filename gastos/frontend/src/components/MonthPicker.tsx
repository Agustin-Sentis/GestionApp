import { useState } from 'react'
import { MAX_MONTH, MIN_MONTH, currentMonthValue, formatMonth, monthName, parseMonthValue } from '../format.ts'
import PickerField from './PickerField.tsx'

interface Props {
  label: string
  /** The label is read by a screen but not drawn (the month in the header sits between two arrows that explain it). */
  hideLabel?: boolean
  /** yyyy-MM */
  value: string
  onChange: (value: string) => void
  className?: string
}

const monthValue = (year: number, month: number) => `${year}-${String(month).padStart(2, '0')}`
const MIN_YEAR = parseMonthValue(MIN_MONTH).year
const MAX_YEAR = parseMonthValue(MAX_MONTH).year

/** The month of the year as a short word for the button ("mar"), the whole name is for the screen reader. */
const shortName = (month: number) => monthName(month).slice(0, 3)

/**
 * A month field with its own picker: a year with arrows and the twelve months. The value is the same yyyy-MM text
 * the native month field gave. It is the partner of DatePicker, which draws days.
 */
export default function MonthPicker({ label, hideLabel, value, onChange, className }: Props) {
  return (
    <PickerField
      label={label}
      hideLabel={hideLabel}
      display={value ? formatMonth(value) : ''}
      placeholder="mm/aaaa"
      className={className}
    >
      {(close) => (
        <MonthGrid
          value={value}
          onChoose={(next) => {
            onChange(next)
            close()
          }}
        />
      )}
    </PickerField>
  )
}

function MonthGrid({ value, onChoose }: { value: string; onChoose: (month: string) => void }) {
  const current = currentMonthValue()
  // The year being looked at starts on the one of the value (or this year), and the arrows move it without choosing anything
  const [year, setYear] = useState(parseMonthValue(value || current).year)

  return (
    <div className="w-64 max-w-full">
      <div className="mb-1 flex items-center justify-between">
        <button
          type="button"
          className="btn px-2.5 py-0.5 pointer-coarse:min-h-11 pointer-coarse:min-w-11"
          aria-label="Año anterior"
          disabled={year <= MIN_YEAR}
          onClick={() => setYear(year - 1)}
        >
          ‹
        </button>
        <span className="figure text-sm font-bold" aria-live="polite">
          {year}
        </span>
        <button
          type="button"
          className="btn px-2.5 py-0.5 pointer-coarse:min-h-11 pointer-coarse:min-w-11"
          aria-label="Año siguiente"
          disabled={year >= MAX_YEAR}
          onClick={() => setYear(year + 1)}
        >
          ›
        </button>
      </div>

      <div className="grid grid-cols-3 gap-1">
        {Array.from({ length: 12 }, (_, index) => {
          const month = index + 1
          const monthText = monthValue(year, month)
          const selected = monthText === value
          return (
            <button
              key={month}
              type="button"
              aria-label={`${monthName(month)} de ${year}`}
              aria-pressed={selected}
              disabled={monthText < MIN_MONTH || monthText > MAX_MONTH}
              autoFocus={selected}
              onClick={() => onChoose(monthText)}
              className={`cursor-pointer rounded-xs border-2 px-1 py-1.5 text-sm font-semibold capitalize pointer-coarse:min-h-11 focus-visible:outline-3 focus-visible:outline-offset-1 focus-visible:outline-pen disabled:cursor-not-allowed disabled:opacity-30 ${
                selected
                  ? 'border-pen bg-pen text-on-pen'
                  : `${monthText === current ? 'border-ink' : 'border-transparent'} hover:bg-mark hover:text-on-mark`
              }`}
            >
              {shortName(month)}
            </button>
          )
        })}
      </div>

      <div className="mt-1 border-t-2 border-line pt-1">
        <button type="button" className="btn-link" onClick={() => onChoose(current)}>
          Este mes
        </button>
      </div>
    </div>
  )
}
