import type { Slice } from '../chartData.ts'
import { formatMoney, formatPercent } from '../format.ts'

interface Props {
  slices: Slice[]
  /** Header of the percent column, for example "% de los ingresos": it says what the 100% is. */
  percentLabel: string
}

/**
 * The figures behind a chart, as a table. It is the chart's legend (the swatch of each row is the color of its mark)
 * and the way to read every value without a mouse or without telling colors apart.
 */
export default function BreakdownTable({ slices, percentLabel }: Props) {
  return (
    <table className="w-full border-collapse text-sm">
      <caption className="sr-only">Detalle del gráfico</caption>
      <thead>
        <tr className="muted text-left text-xs">
          <th className="p-2 font-semibold">Categoría</th>
          <th className="p-2 text-right font-semibold">Monto</th>
          <th className="p-2 text-right font-semibold">{percentLabel}</th>
        </tr>
      </thead>
      <tbody>
        {slices.map((slice) => (
          <tr key={slice.key} className="border-t border-line">
            <td className="p-2">
              <span className="inline-flex items-center gap-2">
                <span aria-hidden="true" className="size-3 shrink-0 rounded-sm" style={{ backgroundColor: slice.color }} />
                {slice.name}
                {slice.detail && <span className="muted text-xs">{slice.detail}</span>}
              </span>
            </td>
            <td className="figure p-2 text-right text-xs whitespace-nowrap">{formatMoney(slice.value)}</td>
            <td className="figure p-2 text-right text-xs whitespace-nowrap">{formatPercent(slice.percent)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
