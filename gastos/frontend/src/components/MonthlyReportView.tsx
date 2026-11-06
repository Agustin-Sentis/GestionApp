import { useQuery } from '@tanstack/react-query'
import { lazy, Suspense, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api.ts'
import { buildChartView, categoryOrder, CHART_KINDS, defaultChartKind, isChartKind } from '../chartData.ts'
import type { ChartKind } from '../chartData.ts'
import { formatMoney } from '../format.ts'
import BreakdownTable from './BreakdownTable.tsx'
import { Skeleton, SkeletonGroup } from './Skeleton.tsx'

// The chart library is heavy: it is downloaded the first time a chart is needed, not with the rest of the app
const ReportChart = lazy(() => import('./ReportChart.tsx'))

/** The URL parameter that remembers which chart is open, so a reload or a shared link shows the same one. */
const CHART_PARAM = 'grafico'

export default function MonthlyReportView({ year, month }: { year: number; month: number }) {
  const [params, setParams] = useSearchParams()

  const { data, isLoading, error } = useQuery({
    queryKey: ['report', year, month],
    queryFn: () => api.reports.monthly(year, month),
  })

  // The same query the rest of the page uses. A category keeps its color in every chart because the color comes from its place here
  const { data: categories } = useQuery({ queryKey: ['categories'], queryFn: api.categories.list })
  const order = useMemo(() => categoryOrder(categories ?? []), [categories])

  if (isLoading) {
    return (
      <section className="card">
        <h2 className="panel-title">Resumen del mes</h2>
        <SkeletonGroup label="Cargando el resumen…" className="flex flex-col gap-3">
          {/* Same shape as the loaded card: the chooser and the chart */}
          <Skeleton className="h-10 w-48" />
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

  // What the user picked, if it is a chart that exists. Otherwise the one that tells the most about this month
  const requested = params.get(CHART_PARAM)
  const kind: ChartKind = isChartKind(requested) ? requested : defaultChartKind(data)
  const view = buildChartView(kind, data, order)

  function chooseKind(next: ChartKind) {
    setParams(
      (previous) => {
        const updated = new URLSearchParams(previous)
        updated.set(CHART_PARAM, next)
        return updated
      },
      { replace: true },
    )
  }

  return (
    <section className="card">
      <h2 className="panel-title">Resumen del mes</h2>

      {data.usdRateUsed !== null && (
        <p className="muted text-sm">
          Los movimientos en USD se convirtieron a pesos con la cotización {formatMoney(data.usdRateUsed)}.
        </p>
      )}

      <label className="field self-start">
        Gráfico
        <select className="input pr-6" value={kind} onChange={(e) => chooseKind(e.target.value as ChartKind)}>
          {CHART_KINDS.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>

      {view.shape === 'empty' ? (
        <p className="muted">{view.message}</p>
      ) : (
        <>
          {view.note && <p className="muted text-sm">{view.note}</p>}
          <Suspense
            fallback={
              <SkeletonGroup label="Cargando el gráfico…">
                <Skeleton className="h-[17.5rem]" />
              </SkeletonGroup>
            }
          >
            <ReportChart view={view} />
          </Suspense>
          <div className="overflow-x-auto">
            <BreakdownTable slices={view.slices} percentLabel={view.percentLabel} />
          </div>
        </>
      )}
    </section>
  )
}
