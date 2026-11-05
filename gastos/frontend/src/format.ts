export function formatMoney(amount: number, currency = 'ARS'): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency }).format(amount)
}

/** Fecha de hoy en hora local (toISOString devolvería la fecha UTC y se corre de día de noche). */
export function todayLocal(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** Mes actual con el formato de <input type="month">: yyyy-MM. */
export function currentMonthValue(): string {
  return todayLocal().slice(0, 7)
}

export function parseMonthValue(value: string): { year: number; month: number } {
  const [year, month] = value.split('-').map(Number)
  return { year, month }
}

/** yyyy-MM-dd → dd/MM/yyyy */
export function formatDate(iso: string): string {
  const [y, m, d] = iso.split('-')
  return `${d}/${m}/${y}`
}
