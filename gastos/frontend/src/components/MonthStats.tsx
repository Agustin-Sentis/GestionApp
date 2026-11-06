import { useQuery } from '@tanstack/react-query'
import { api } from '../api.ts'
import { formatMoneyCompact } from '../format.ts'
import { Skeleton, SkeletonGroup } from './Skeleton.tsx'

/**
 * The three figures of the month, written like the totals at the foot of a ledger page. It asks for the same report as
 * the summary panel (same query key), so the two share one request and always agree.
 */
export default function MonthStats({ year, month }: { year: number; month: number }) {
  const { data, isLoading } = useQuery({
    queryKey: ['report', year, month],
    queryFn: () => api.reports.monthly(year, month),
  })

  if (isLoading) {
    return (
      <SkeletonGroup label="Cargando las cifras del mes…" className="grid max-w-md grid-cols-3 gap-3 sm:gap-6">
        <Skeleton className="h-11" />
        <Skeleton className="h-11" />
        <Skeleton className="h-11" />
      </SkeletonGroup>
    )
  }
  // If the report failed, the summary panel is the one that explains it
  if (!data) return null

  const stillHave = data.balance >= 0

  return (
    // A figure is never cut in the middle: if the three do not fit in a row, the last one goes down to the next line
    <dl className="flex flex-wrap gap-x-6 gap-y-2">
      <Figure label="Ingresos" value={formatMoneyCompact(data.income)} className="text-income" />
      <Figure label="Gastos" value={formatMoneyCompact(data.expense)} className="text-expense" />
      <Figure
        label={stillHave ? 'Te queda' : 'Te pasaste'}
        value={formatMoneyCompact(Math.abs(data.balance))}
        // The result of the month is the one number that gets the highlighter and the ledger's double underline
        className="marker border-b-[3px] border-double border-ink font-bold"
      />
    </dl>
  )
}

function Figure({ label, value, className }: { label: string; value: string; className: string }) {
  return (
    <div className="flex flex-col gap-0.5">
      <dt className="muted text-[0.7rem] font-semibold tracking-wide uppercase">{label}</dt>
      <dd>
        <span className={`figure text-sm whitespace-nowrap sm:text-lg ${className}`}>{value}</span>
      </dd>
    </div>
  )
}
