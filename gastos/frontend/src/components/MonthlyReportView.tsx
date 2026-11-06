import { useQuery } from '@tanstack/react-query'
import { lazy, Suspense } from 'react'
import { api, EXPENSE, INCOME } from '../api.ts'
import { formatMoney } from '../format.ts'
import { Skeleton, SkeletonGroup } from './Skeleton.tsx'

// The chart library is heavy: it is downloaded the first time a chart is needed, not with the rest of the app
const ExpensePieChart = lazy(() => import('./ExpensePieChart.tsx'))

export default function MonthlyReportView({ year, month }: { year: number; month: number }) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['report', year, month],
    queryFn: () => api.reports.monthly(year, month),
  })

  if (isLoading) {
    return (
      <section className="card">
        <h2 className="font-semibold">Resumen del mes</h2>
        <SkeletonGroup label="Cargando el resumen…" className="flex flex-col gap-3">
          {/* Same shape as the loaded card: three figures and the chart */}
          <div className="grid grid-cols-3 gap-2">
            <Skeleton className="h-[4.5rem]" />
            <Skeleton className="h-[4.5rem]" />
            <Skeleton className="h-[4.5rem]" />
          </div>
          <Skeleton className="h-[17.5rem]" />
        </SkeletonGroup>
      </section>
    )
  }
  if (error || !data) {
    return (
      <section className="card">
        <p className="error-text">{error instanceof Error ? error.message : 'No se pudo cargar el reporte'}</p>
      </section>
    )
  }

  const expenses = data.byCategory.filter((c) => c.type === EXPENSE && c.total > 0)
  const hasIncome = data.byCategory.some((c) => c.type === INCOME)

  return (
    <section className="card">
      <h2 className="font-semibold">Resumen del mes</h2>

      <div className="grid grid-cols-3 gap-2">
        <Stat label="Ingresos" value={formatMoney(data.income)} tone="income" />
        <Stat label="Gastos" value={formatMoney(data.expense)} tone="expense" />
        <Stat label="Balance" value={formatMoney(data.balance)} tone={data.balance >= 0 ? 'income' : 'expense'} />
      </div>

      {data.usdRateUsed !== null && (
        <p className="muted text-sm">
          Los movimientos en USD se convirtieron a pesos con la cotización {formatMoney(data.usdRateUsed)}.
        </p>
      )}

      {expenses.length === 0 ? (
        <p className="muted">
          {hasIncome ? 'No hay gastos cargados en este mes.' : 'Todavía no hay movimientos cargados en este mes.'}
        </p>
      ) : (
        <Suspense
          fallback={
            <SkeletonGroup label="Cargando el gráfico…">
              <Skeleton className="h-[17.5rem]" />
            </SkeletonGroup>
          }
        >
          <ExpensePieChart expenses={expenses} />
        </Suspense>
      )}
    </section>
  )
}

function Stat({ label, value, tone }: { label: string; value: string; tone: 'income' | 'expense' }) {
  const color = tone === 'income' ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400'
  return (
    <div className="flex min-w-0 flex-col rounded-lg border border-slate-200 p-3 dark:border-slate-800">
      <span className="muted text-sm">{label}</span>
      <strong className={`wrap-anywhere text-lg ${color}`}>{value}</strong>
    </div>
  )
}
