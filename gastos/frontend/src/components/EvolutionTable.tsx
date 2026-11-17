import type { PeriodTotals } from '../api.ts'
import type { EvolutionPoint } from '../evolutionData.ts'
import { formatMonth, formatMoney } from '../format.ts'

interface Props {
  points: EvolutionPoint[]
  totals: PeriodTotals
  average: PeriodTotals
}

/** A balance below zero is written with the word as well as the color: the color alone is not read by everybody. */
function Balance({ value }: { value: number }) {
  return (
    <>
      <span className={value < 0 ? 'text-expense' : undefined}>{formatMoney(value)}</span>
      {value < 0 && <span className="muted ml-1 text-xs">déficit</span>}
    </>
  )
}

/**
 * The figures behind the evolution chart, month by month, with the total and the monthly average at the end. It is the
 * chart's text version: the chart is hidden from screen readers and this table says everything it draws.
 */
export default function EvolutionTable({ points, totals, average }: Props) {
  return (
    <table className="w-full border-collapse text-sm">
      <caption className="sr-only">Ingresos, gastos y saldo de cada mes</caption>
      <thead>
        <tr className="muted text-left text-xs">
          <th scope="col" className="p-2 font-semibold">Mes</th>
          <th scope="col" className="p-2 text-right font-semibold">Ingresos</th>
          <th scope="col" className="p-2 text-right font-semibold">Gastos</th>
          <th scope="col" className="p-2 text-right font-semibold">Saldo</th>
        </tr>
      </thead>
      <tbody>
        {points.map((point) => (
          <tr key={point.key} className="border-t border-line">
            <th scope="row" className="p-2 text-left font-normal">
              {/* The short label fits the narrow column; the month in words is for whoever listens to the page */}
              <span aria-hidden="true">{point.label}</span>
              <span className="sr-only">{formatMonth(point.key)}</span>
              {point.incomplete && <span className="muted ml-1 text-xs">(en curso)</span>}
              {point.unconverted && <span className="error-text ml-1 text-xs">incompleto</span>}
            </th>
            <td className="figure p-2 text-right text-xs whitespace-nowrap">{formatMoney(point.income)}</td>
            <td className="figure p-2 text-right text-xs whitespace-nowrap">{formatMoney(point.expense)}</td>
            <td className="figure p-2 text-right text-xs whitespace-nowrap"><Balance value={point.balance} /></td>
          </tr>
        ))}
      </tbody>
      <tfoot>
        <tr className="border-t-2 border-ink font-bold">
          <th scope="row" className="p-2 text-left">Total</th>
          <td className="figure p-2 text-right text-xs whitespace-nowrap">{formatMoney(totals.income)}</td>
          <td className="figure p-2 text-right text-xs whitespace-nowrap">{formatMoney(totals.expense)}</td>
          <td className="figure p-2 text-right text-xs whitespace-nowrap"><Balance value={totals.balance} /></td>
        </tr>
        <tr className="border-t border-line">
          <th scope="row" className="p-2 text-left font-semibold">Promedio mensual</th>
          <td className="figure p-2 text-right text-xs whitespace-nowrap">{formatMoney(average.income)}</td>
          <td className="figure p-2 text-right text-xs whitespace-nowrap">{formatMoney(average.expense)}</td>
          <td className="figure p-2 text-right text-xs whitespace-nowrap"><Balance value={average.balance} /></td>
        </tr>
      </tfoot>
    </table>
  )
}
