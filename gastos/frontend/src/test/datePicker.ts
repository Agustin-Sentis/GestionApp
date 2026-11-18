import { screen, within } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'
import { monthName } from '../shared/format.ts'

/** The name of the button of a DatePicker: it starts with the label of the field ("Fecha", "Desde"...), then comes the date. */
const buttonName = (label: string) => new RegExp(`^${label}\\b`)

/** The button that shows the date of a DatePicker. */
export function dateButton(label: string): HTMLElement {
  return screen.getByRole('button', { name: buttonName(label) })
}

/** The same, waiting for it to be on screen (the form draws after its categories load). */
export function findDateButton(label: string): Promise<HTMLElement> {
  return screen.findByRole('button', { name: buttonName(label) })
}

/** Chooses a day (yyyy-MM-dd) in a DatePicker the way a person does: opens it, picks the year and the month, and presses the day. */
export async function pickDate(user: UserEvent, label: string, iso: string) {
  await user.click(dateButton(label))
  const calendar = screen.getByRole('dialog', { name: label })
  const [year, month, day] = iso.split('-').map(Number)

  await user.selectOptions(within(calendar).getByRole('combobox', { name: 'Elegir el año' }), String(year))
  await user.selectOptions(within(calendar).getByRole('combobox', { name: 'Elegir el mes' }), String(month - 1))
  // The name of a day is "viernes, 5 de noviembre de 2026"; the days of the neighbor months drawn in the grid have another month in theirs
  await user.click(within(calendar).getByRole('button', { name: new RegExp(`\\b${day} de ${monthName(month)} de ${year}`) }))
}

/** Chooses a month (yyyy-MM) in a MonthPicker: opens it, moves the year with its arrows until it is the right one, and presses the month. */
export async function pickMonth(user: UserEvent, label: string, month: string) {
  await user.click(dateButton(label))
  const picker = screen.getByRole('dialog', { name: label })
  const [year, monthNumber] = month.split('-').map(Number)

  const shownYear = () => Number(within(picker).getByText(/^\d{4}$/).textContent)
  while (shownYear() !== year) {
    await user.click(within(picker).getByRole('button', { name: shownYear() < year ? 'Año siguiente' : 'Año anterior' }))
  }
  await user.click(within(picker).getByRole('button', { name: `${monthName(monthNumber)} de ${year}` }))
}
