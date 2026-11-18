import { DayPicker } from 'react-day-picker'
import { es } from 'react-day-picker/locale'
import 'react-day-picker/style.css'
import './DatePicker.css' // after the library's: it takes the colors and shapes of the app
import { MAX_MONTH, MIN_MONTH, formatDate, parseDateValue, parseMonthValue, todayLocal, toDateValue } from '../../shared/format.ts'
import PickerField from '../PickerField/PickerField.tsx'

interface Props {
  label: string
  /** yyyy-MM-dd, or '' when there is no date (only possible when `clearable`). */
  value: string
  onChange: (value: string) => void
  /** Offers "Borrar", to leave the field empty (a filter can be empty; the date of a movement cannot). */
  clearable?: boolean
  /** What is wrong with the date, if anything: it is written under the field. */
  error?: string
  className?: string
}

interface CalendarProps {
  value: string
  clearable: boolean
  /** Receives the chosen day (yyyy-MM-dd, or '' to leave the field empty). */
  onChoose: (value: string) => void
}

/** The first day of a yyyy-MM, as the Date the calendar uses for its limits. */
const monthStart = (value: string): Date => {
  const { year, month } = parseMonthValue(value)
  return new Date(year, month - 1, 1)
}

/** What opens under the field: the calendar with its "Hoy" and "Borrar". */
const DateCalendar = ({ value, clearable, onChoose }: CalendarProps) => {
  const selected = parseDateValue(value)
  const today = parseDateValue(todayLocal()) as Date

  // Pressing the chosen day again deselects it: that empties a filter, and changes nothing in a date that must exist
  const handleSelectDay = (date: Date | undefined) => onChoose(date ? toDateValue(date) : clearable ? '' : value)

  const handleChooseToday = () => onChoose(todayLocal())

  const handleClear = () => onChoose('')

  return (
    <>
      <DayPicker
        mode="single"
        locale={es}
        selected={selected}
        onSelect={handleSelectDay}
        defaultMonth={selected ?? today}
        startMonth={monthStart(MIN_MONTH)}
        endMonth={monthStart(MAX_MONTH)}
        captionLayout="dropdown"
        showOutsideDays
        fixedWeeks
        autoFocus
      />
      <div className="mt-1 flex items-center justify-between border-t-2 border-line pt-1">
        <button type="button" className="btn-link" onClick={handleChooseToday}>
          Hoy
        </button>
        {clearable && (
          <button type="button" className="btn-link" onClick={handleClear} disabled={!selected}>
            Borrar
          </button>
        )}
      </div>
    </>
  )
}

/**
 * A date field with the app's own calendar. The value is the same yyyy-MM-dd text the native field gave,
 * so whoever uses it (and the server) notices no difference.
 */
const DatePicker = ({ label, value, onChange, clearable = false, error, className }: Props) => {
  const selected = parseDateValue(value)

  // Choosing closes the calendar and gives the focus back to the field
  const handleChoose = (close: () => void) => (next: string) => {
    onChange(next)
    close()
  }

  return (
    <PickerField label={label} display={selected ? formatDate(value) : ''} placeholder="dd/mm/aaaa" error={error} className={className}>
      {(close) => <DateCalendar value={value} clearable={clearable} onChoose={handleChoose(close)} />}
    </PickerField>
  )
}

export default DatePicker
