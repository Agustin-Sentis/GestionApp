import type { PendingConversion } from '../../Services/api/api.ts'
import { DOLLAR_NAMES } from '../../shared/dollar.ts'
import { formatMoney } from '../../shared/format.ts'

/**
 * A movement saved without the rate of its dollar is converted with today's, and a report says so for each dollar.
 * When even today's rate could not be obtained, those movements are not in the totals, and that is said too.
 * Shared by the monthly summary and the evolution: both convert the same way.
 */
const PendingConversionNotes = ({ pending }: { pending: PendingConversion[] }) => {
  return (
    <>
      {pending.map((item) =>
        item.rate !== null ? (
          <p key={item.dollarType} className="muted text-sm">
            Algunos movimientos en USD guardados con el dólar {DOLLAR_NAMES[item.dollarType]} no tenían su cotización: se
            convirtieron con la de hoy, {formatMoney(item.rate)}.
          </p>
        ) : (
          <p key={item.dollarType} className="error-text" role="status">
            No se pudo obtener la cotización {DOLLAR_NAMES[item.dollarType]}: algunos movimientos en USD no están sumados
            en estos totales.
          </p>
        ),
      )}
    </>
  )
}

export default PendingConversionNotes
