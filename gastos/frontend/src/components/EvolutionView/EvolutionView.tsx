import { useQuery } from '@tanstack/react-query'
import { lazy, Suspense, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../../Services/api/api.ts'
import { buildEvolutionView } from '../../shared/evolutionData.ts'
import { FROM_PARAM, PRESET_PARAM, resolveRange, TO_PARAM } from '../../shared/evolutionRange.ts'
import type { MonthRange, RangePreset } from '../../shared/evolutionRange.ts'
import EmptyNote from '../EmptyNote/EmptyNote.tsx'
import EvolutionTable from '../EvolutionTable/EvolutionTable.tsx'
import PendingConversionNotes from '../PendingConversionNotes/PendingConversionNotes.tsx'
import RangePicker from '../RangePicker/RangePicker.tsx'
import { Skeleton, SkeletonGroup } from '../Skeleton/Skeleton.tsx'

// The chart library is heavy: it is downloaded the first time a chart is needed, not with the rest of the app
const EvolutionChart = lazy(() => import('../EvolutionChart/EvolutionChart.tsx'))

/**
 * How the months have evolved: income, expenses and balance month by month over a range. The range lives in the address
 * (a shortcut, or desde and hasta), so a reload or a shared link shows the same thing. The shortcuts end at
 * `selectedMonth`, the month picked at the top of the page.
 */
const EvolutionView = ({ selectedMonth }: { selectedMonth: string }) => {
  const [params, setParams] = useSearchParams()
  const { range, error: rangeError, mode } = resolveRange(params, selectedMonth)

  // Starts with 'report' so everything that already refreshes the reports (saving or deleting a movement) refreshes this too
  const { data, isLoading, error } = useQuery({
    queryKey: ['report', 'evolution', range?.from, range?.to],
    queryFn: () => api.reports.evolution(range!.from, range!.to),
    // A range that cannot be asked for is explained instead
    enabled: range !== null,
  })

  const view = useMemo(() => (data ? buildEvolutionView(data) : null), [data])

  // Changes the address without leaving a new entry in the history
  const updateParams = (change: (next: URLSearchParams) => void) => {
    setParams(
      (previous) => {
        const next = new URLSearchParams(previous)
        change(next)
        return next
      },
      { replace: true },
    )
  }

  const handleChoosePreset = (preset: RangePreset) => {
    updateParams((next) => {
      next.delete(FROM_PARAM)
      next.delete(TO_PARAM)
      next.set(PRESET_PARAM, preset)
    })
  }

  const handleChooseRange = (chosen: MonthRange) => {
    updateParams((next) => {
      next.delete(PRESET_PARAM)
      next.set(FROM_PARAM, chosen.from)
      next.set(TO_PARAM, chosen.to)
    })
  }

  return (
    <section className="card">
      <h2 className="panel-title">Evolución</h2>

      <RangePicker
        mode={mode}
        // When the range cannot be asked for the fields keep what the user wrote, so it can be fixed
        from={range?.from ?? params.get(FROM_PARAM) ?? ''}
        to={range?.to ?? params.get(TO_PARAM) ?? ''}
        onPreset={handleChoosePreset}
        onCustom={handleChooseRange}
      />

      {rangeError && (
        <p className="error-text" role="status">
          {rangeError}
        </p>
      )}

      {range && isLoading && (
        <SkeletonGroup label="Cargando la evolución…" className="flex flex-col gap-3">
          {/* Same shape as the loaded card: the chart and the table */}
          <Skeleton className="h-[17.5rem]" />
          <Skeleton className="h-24" />
        </SkeletonGroup>
      )}

      {range && !isLoading && (error || !data || !view) && (
        <p className="error-text">{error instanceof Error ? error.message : 'No se pudo cargar la evolución'}</p>
      )}

      {data && view && (
        <>
          <PendingConversionNotes pending={data.pendingConversions} />

          {view.shape === 'empty' ? (
            <EmptyNote>{view.message}</EmptyNote>
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
                <EvolutionChart points={view.points} />
              </Suspense>
              <div className="overflow-x-auto">
                <EvolutionTable points={view.points} totals={view.totals} average={view.average} />
              </div>
            </>
          )}
        </>
      )}
    </section>
  )
}

export default EvolutionView
