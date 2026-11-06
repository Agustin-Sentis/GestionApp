import { formatMoney } from './format.ts'

/** A line of the example sheet. Invented numbers, but the arithmetic is real: the totals are added up from these lines. */
export interface SampleLine {
  date: string
  concept: string
  /** Pesos. Positive is income, negative is expense. */
  amount: number
  /** A note written by hand beside the line. */
  aside?: string
}

const USD_AMOUNT = 12
const USD_RATE = 1300

export const SAMPLE_LINES: readonly SampleLine[] = [
  { date: '2026-11-03', concept: 'Supermercado', amount: -48200 },
  { date: '2026-11-04', concept: 'Sueldo', amount: 900000 },
  { date: '2026-11-05', concept: 'Alquiler', amount: -310000 },
  {
    date: '2026-11-07',
    concept: `Suscripción (US$ ${USD_AMOUNT})`,
    amount: -(USD_AMOUNT * USD_RATE),
    aside: `cotización del día: ${formatMoney(USD_RATE)}`,
  },
]

export function sampleTotals(lines: readonly SampleLine[] = SAMPLE_LINES) {
  const income = lines.filter((l) => l.amount > 0).reduce((acc, l) => acc + l.amount, 0)
  const expense = lines.filter((l) => l.amount < 0).reduce((acc, l) => acc - l.amount, 0)
  return { income, expense, left: income - expense }
}

/** What the app does, in the words of someone using it. Each entry is something that exists today. */
export const CAPABILITIES: readonly { concept: string; what: string }[] = [
  {
    concept: 'Pesos y dólares',
    what: 'Anotá cada movimiento en ARS o USD. Elegí con qué dólar se guardan los nuevos (oficial, blue o MEP): cada movimiento conserva la cotización del día en que lo cargaste y el dólar con el que se guardó, así los meses cerrados no cambian.',
  },
  {
    concept: 'Tus categorías',
    what: 'Creá las de gastos y las de ingresos. Una categoría con movimientos no se puede borrar, así no perdés nada por accidente.',
  },
  {
    concept: 'El mes de un vistazo',
    what: 'Cuánto entró, cuánto salió y cuánto te queda, mes por mes, siempre a la vista arriba de todo.',
  },
  {
    concept: 'Cinco gráficos',
    what: 'Destino de los ingresos, gastos por categoría, ingresos por categoría, ranking de categorías e ingresos contra gastos. Cada uno con su tabla de montos y porcentajes.',
  },
  {
    concept: 'Buscar y filtrar',
    what: 'Por mes o rango de fechas, categoría, tipo y texto de la nota. Cada vista tiene su propio enlace: guardala o compartila.',
  },
  {
    concept: 'Excel',
    what: 'Bajá lo que estás viendo en un archivo CSV que Excel abre bien en español. Hasta 10.000 movimientos.',
  },
  {
    concept: 'Hoja o pizarrón',
    what: 'Elegí el modo claro, el oscuro (el pizarrón) o el que use tu dispositivo.',
  },
  {
    concept: 'Solo tuyo',
    what: 'Cada cuenta ve únicamente sus propios movimientos, y la contraseña se guarda cifrada.',
  },
]

/** Things already on the list, written the way you note something for later. */
export const LATER: readonly string[] = [
  'presupuestos por categoría, con aviso cuando te pasás',
  'movimientos que se repiten (alquiler, suscripciones)',
  'reportes por rango de meses, comparados con el anterior',
]

