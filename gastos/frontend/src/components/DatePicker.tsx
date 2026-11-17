import { DayPicker } from 'react-day-picker'
import { es } from 'react-day-picker/locale'
import 'react-day-picker/style.css'
import '../datePicker.css' // after the library's: it takes the colors and shapes of the app
import { MAX_MONTH, MIN_MONTH, formatDate, parseDateValue, parseMonthValue, todayLocal, toDateValue } from '../format.ts'
import PickerField from './PickerField.tsx'

interface Props {
  label: string
  /** yyyy-MM-dd, or '' when there is no date (only possible when `clearable`). */
  value: string
  onChange: (value: string) => void
  /** Offers "Borrar", to leave the field empty (a filter can be empty; the date of a movement cannot). */
  clearable?: boolean
  className?: string
}

/** The first day of a yyyy-MM, as the Date the calendar uses for its limits. */
function monthStart(value: string): Date {
  const { year, month } = parseMonthValue(value)
  return new Date(year, month - 1, 1)
}

/**
 * A date field with the app's own calendar. The value is the same yyyy-MM-dd text the native field gave,
 * so whoever uses it (and the server) notices no difference.
 */
export default function DatePicker({ label, value, onChange, clearable = false, className }: Props) {
  const selected = parseDateValue(value)
  const today = parseDateValue(todayLocal()) as Date

  return (
    <PickerField label={label} display={selected ? formatDate(value) : ''} placeholder="dd/mm/aaaa" className={className}>
      {(close) => {
        const choose = (next: string) => {
          onChange(next)
          close()
        }
        return (
          <>
            <DayPicker
              mode="single"
              locale={es}
              selected={selected}
              // Pressing the chosen day again deselects it: that empties a filter, and changes nothing in a date that must exist
              onSelect={(date) => choose(date ? toDateValue(date) : clearable ? '' : value)}
              defaultMonth={selected ?? today}
              startMonth={monthStart(MIN_MONTH)}
              endMonth={monthStart(MAX_MONTH)}
              captionLayout="dropdown"
              showOutsideDays
              fixedWeeks
              autoFocus
            />
            <div className="mt-1 flex items-center justify-between border-t-2 border-line pt-1">
              <button type="button" className="btn-link" onClick={() => choose(todayLocal())}>
                Hoy
              </button>
              {clearable && (
                <button type="button" className="btn-link" onClick={() => choose('')} disabled={!selected}>
                  Borrar
                </button>
              )}
            </div>
          </>
        )
      }}
    </PickerField>
  )
}
