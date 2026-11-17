import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { api, EXPENSE, INCOME } from '../api.ts'
import type { Transaction, TransactionType } from '../api.ts'
import { DOLLAR_NAMES } from '../dollar.ts'
import { downloadBlob, exportFileName } from '../download.ts'
import { formatDate, formatMoney, formatMonth, todayLocal } from '../format.ts'
import { JUST_ADDED_KEY, JUST_ADDED_MS } from '../justAdded.ts'
import { dayMarker, groupByDay } from '../ledgerDays.ts'
import { SEARCH_MAX_LENGTH, useListFilters } from '../useListFilters.ts'
import ConfirmDelete from './ConfirmDelete.tsx'
import DatePicker from './DatePicker.tsx'
import EmptyNote from './EmptyNote.tsx'
import { Skeleton, SkeletonGroup } from './Skeleton.tsx'
import { useToast } from './toastContext.ts'

/** How long the user must stop typing before the list is asked for the text, so each keystroke is not a request. */
const SEARCH_DEBOUNCE_MS = 300

/**
 * The margin of the ledger: the day of the movements next to it, written big, between the page and the red line.
 * One cell for the whole day (it spans its rows). The full date goes to screen readers and to the tooltip, because
 * the margin only has room for the day and the month.
 */
function DayMargin({ date, rows }: { date: string; rows: number }) {
  const marker = dayMarker(date, Number(todayLocal().slice(0, 4)))
  return (
    <td rowSpan={rows} className="w-12 border-r-2 border-margin px-1 py-2 text-center align-top sm:w-14 sm:px-2">
      <time dateTime={date} title={formatDate(date)}>
        <span className="sr-only">{formatDate(date)}</span>
        <span aria-hidden="true" className="display block text-xl leading-none">
          {marker.day}
        </span>
        <span aria-hidden="true" className="muted block text-xs uppercase">
          {marker.month}
          {marker.year && ` ${marker.year}`}
        </span>
      </time>
    </td>
  )
}

interface Props {
  /** Called when the user asks to edit a row. */
  onEdit?: (transaction: Transaction) => void
  /** The row being edited, to mark it. */
  editingId?: string | null
  /** Called after a transaction was deleted, so whoever was editing it can let go. */
  onDeleted?: (id: string) => void
  /** Takes the person to the form that adds a movement, to offer it when the month has none. */
  onAddMovement?: () => void
}

export default function TransactionList({ onEdit, editingId = null, onDeleted, onAddMovement }: Props) {
  const queryClient = useQueryClient()
  const toast = useToast()
  const list = useListFilters()
  const { filters, query, rangeInverted, hasExtraFilters } = list

  // What is typed in the search box. The URL (and so the request) follows it after a short pause.
  const [searchText, setSearchText] = useState(filters.search)

  useEffect(() => {
    const text = searchText.trim()
    if (text === filters.search) return
    const timer = setTimeout(() => list.setSearch(text), SEARCH_DEBOUNCE_MS)
    return () => clearTimeout(timer)
    // Only the typed text should restart the pause (setSearch is recreated on every render)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchText])

  // The URL changed by itself (clear filters, back button, a pasted link): the box shows what the list is using
  useEffect(() => {
    setSearchText((current) => (current.trim() === filters.search ? current : filters.search))
  }, [filters.search])

  const { data: categories = [] } = useQuery({ queryKey: ['categories'], queryFn: api.categories.list })

  // The movement the form just added (see justAdded.ts): its row is highlighted for a moment, then it is forgotten
  const { data: justAddedId = null } = useQuery<string | null>({
    queryKey: JUST_ADDED_KEY,
    queryFn: () => null,
    enabled: false,
    staleTime: Infinity,
  })
  useEffect(() => {
    if (!justAddedId) return
    const timer = setTimeout(() => queryClient.setQueryData(JUST_ADDED_KEY, null), JUST_ADDED_MS)
    return () => clearTimeout(timer)
  }, [justAddedId, queryClient])

  const { data, isLoading, isPlaceholderData, error } = useQuery({
    queryKey: ['transactions', query],
    queryFn: () => api.transactions.listPaged(query),
    // An inverted range would be answered with a 400: show the explanation instead of asking
    enabled: !rangeInverted,
    // Keep showing the previous page until the next one arrives, instead of flashing placeholders on every click
    placeholderData: keepPreviousData,
  })

  const remove = useMutation({
    mutationFn: (id: string) => api.transactions.remove(id),
    onSuccess: (_result, id) => {
      void queryClient.invalidateQueries({ queryKey: ['transactions'] })
      void queryClient.invalidateQueries({ queryKey: ['report'] })
      toast.success('Movimiento eliminado')
      onDeleted?.(id)
    },
    onError: (error) => toast.error(error.message),
  })

  const exportCsv = useMutation({
    mutationFn: () => api.transactions.exportCsv(query),
    onSuccess: (file) => {
      downloadBlob(file, exportFileName(filters))
      toast.success('Archivo descargado')
    },
    onError: (error) => toast.error(error.message),
  })

  // After deleting the last item of the last page, that page no longer exists: step back to the one before
  useEffect(() => {
    if (!data || isPlaceholderData) return
    const lastPage = Math.max(1, data.totalPages)
    if (filters.page > lastPage) list.setPage(lastPage)
    // setPage is recreated on every render; only the page and the data matter here
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, isPlaceholderData, filters.page])

  const expenseCategories = categories.filter((c) => c.type === EXPENSE)
  const incomeCategories = categories.filter((c) => c.type === INCOME)

  return (
    <section className="card">
      <h2 className="panel-title">Libro de movimientos</h2>

      <div className="grid grid-cols-2 items-end gap-x-3 gap-y-2">
        <label className="field col-span-2">
          Buscar en la nota
          <input
            className="input"
            type="search"
            value={searchText}
            maxLength={SEARCH_MAX_LENGTH}
            placeholder="Ej.: almuerzo"
            onChange={(e) => setSearchText(e.target.value)}
          />
        </label>

        <label className="field">
          Categoría
          <select
            className="input"
            value={filters.categoryId ?? ''}
            onChange={(e) => list.setCategory(e.target.value || null)}
          >
            <option value="">Todas</option>
            {expenseCategories.length > 0 && (
              <optgroup label="Gastos">
                {expenseCategories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </optgroup>
            )}
            {incomeCategories.length > 0 && (
              <optgroup label="Ingresos">
                {incomeCategories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </optgroup>
            )}
          </select>
        </label>

        <label className="field">
          Tipo
          <select
            className="input"
            value={filters.type ?? ''}
            onChange={(e) => list.setType((e.target.value || null) as TransactionType | null)}
          >
            <option value="">Todos</option>
            <option value={EXPENSE}>Gastos</option>
            <option value={INCOME}>Ingresos</option>
          </select>
        </label>

        {/* One row for the dates of the list and what is done with it: the range on the left, clearing and exporting on the right */}
        <div className="col-span-2 flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
          <label className="inline-flex items-center gap-2 py-1 pointer-coarse:min-h-11 pointer-coarse:py-3 text-sm">
            <input
              className="pointer-coarse:size-5"
              type="checkbox"
              checked={filters.rangeMode}
              onChange={(e) => list.setRangeMode(e.target.checked)}
            />
            Rango de fechas
          </label>

          <div className="flex items-center gap-2">
            {hasExtraFilters && (
              <button type="button" className="btn-link" onClick={list.clear}>
                Limpiar filtros
              </button>
            )}
            <button
              type="button"
              className="btn"
              onClick={() => exportCsv.mutate()}
              disabled={exportCsv.isPending || rangeInverted || !data || data.total === 0}
            >
              {exportCsv.isPending ? 'Exportando…' : 'Exportar CSV'}
            </button>
          </div>
        </div>

        {/* "Desde" and "Hasta" side by side, under the row of the tick */}
        {filters.rangeMode && (
          <>
            <DatePicker label="Desde" value={filters.from} onChange={list.setFrom} clearable />
            <DatePicker label="Hasta" value={filters.to} onChange={list.setTo} clearable />
          </>
        )}
      </div>

      {filters.rangeMode && (
        <p className="muted text-sm">
          Esta lista usa el rango elegido. El resumen de arriba sigue mostrando {formatMonth(filters.month)}.
        </p>
      )}

      {rangeInverted && (
        <p className="error-text" role="alert">
          La fecha «Desde» no puede ser posterior a «Hasta».
        </p>
      )}

      {isLoading && !rangeInverted && (
        <SkeletonGroup label="Cargando los movimientos…" className="flex flex-col gap-2">
          <Skeleton className="h-9" />
          <Skeleton className="h-9" />
          <Skeleton className="h-9" />
          <Skeleton className="h-9" />
        </SkeletonGroup>
      )}
      {error && <p className="error-text">{error instanceof Error ? error.message : 'Error al cargar'}</p>}

      {data && !rangeInverted && data.total === 0 && (
        hasExtraFilters ? (
          <div className="flex flex-col items-start gap-1">
            <p className="muted">Ningún movimiento coincide con los filtros.</p>
            <button type="button" className="btn-link" onClick={list.clear}>
              Limpiar filtros
            </button>
          </div>
        ) : (
          <EmptyNote actionLabel="Agregar un movimiento" onAction={onAddMovement}>
            Todavía no agregaste movimientos este mes.
          </EmptyNote>
        )
      )}

      {data && !rangeInverted && data.total > 0 && (
        <>
          <div className="overflow-x-auto" aria-busy={isPlaceholderData}>
            {/* A page of the ledger: the date goes in the margin (the red line), once per day, and each movement is a ruled line */}
            <table className={`w-full border-collapse text-sm ${isPlaceholderData ? 'opacity-60' : ''}`}>
              <thead>
                <tr className="muted text-left text-xs tracking-wide uppercase">
                  <th className="p-2 font-semibold">Fecha</th>
                  <th className="p-2 font-semibold">Categoría y nota</th>
                  <th className="p-2 text-right font-semibold">Monto</th>
                  <th aria-label="Acciones" />
                </tr>
              </thead>
              <tbody>
                {groupByDay(data.items).map((group) =>
                  group.items.map((t, index) => (
                    <tr
                      key={t.id}
                      aria-current={t.id === editingId ? 'true' : undefined}
                      // The first line of a day is a stronger rule, the others of the same day are fainter
                      className={`${index === 0 ? 'border-t-2 border-line' : 'border-t border-line/50'} ${t.id === editingId ? 'bg-mark/25' : ''} ${t.id === justAddedId ? 'just-added' : ''} ${
                        // Being deleted: crossed out with the pen until the list answers
                        remove.isPending && remove.variables === t.id ? 'struck' : ''
                      }`}
                    >
                      {index === 0 && <DayMargin date={group.date} rows={group.items.length} />}
                      <td className="px-1.5 py-2 sm:p-2">
                        <div>{t.categoryName}</div>
                        {/* The note under the category: one column less, so on a phone the amount fits */}
                        {t.note && <div className="muted">{t.note}</div>}
                      </td>
                      <td className="figure px-1.5 py-2 text-right text-xs whitespace-nowrap sm:p-2 sm:text-sm">
                        <div className={t.type === EXPENSE ? 'text-expense' : 'text-income'}>
                          {t.type === EXPENSE ? '−' : '+'}
                          {formatMoney(t.amount, t.currency)}
                        </div>
                        {t.currency === 'USD' && (
                          <div className="muted ml-auto max-w-56 text-xs whitespace-normal">
                            {t.exchangeRate !== null && t.arsAmount !== null
                              ? `≈ ${formatMoney(t.arsAmount)} · cotización ${DOLLAR_NAMES[t.dollarType]} ${formatMoney(t.exchangeRate)}`
                              : `Sin cotización ${DOLLAR_NAMES[t.dollarType]} todavía`}
                          </div>
                        )}
                      </td>
                      <td className="px-1 py-2 text-right whitespace-nowrap sm:p-2">
                        <div className="inline-flex items-center gap-1 sm:gap-2">
                          {onEdit && (
                            <button
                              type="button"
                              className="btn-link"
                              onClick={() => onEdit(t)}
                              aria-label={`Editar movimiento de ${t.categoryName}`}
                            >
                              {/* A pencil where the word does not fit (a phone); the name for a screen reader is the label above either way */}
                              <svg
                                className="sm:hidden"
                                width="16"
                                height="16"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2"
                                aria-hidden="true"
                              >
                                <path d="M4 20h4L19 9l-4-4L4 16v4ZM13.5 6.5l4 4" />
                              </svg>
                              <span className="hidden sm:inline">Editar</span>
                            </button>
                          )}
                          <ConfirmDelete
                            label={`Eliminar movimiento de ${t.categoryName}`}
                            onConfirm={() => remove.mutate(t.id)}
                            pending={remove.isPending}
                          />
                        </div>
                      </td>
                    </tr>
                  )),
                )}
              </tbody>
            </table>
          </div>

          <nav aria-label="Paginación" className="flex flex-wrap items-center justify-between gap-2 text-sm">
            <p className="muted">
              {data.totalPages > 1 && `Página ${data.page} de ${data.totalPages} · `}
              {data.total === 1 ? '1 movimiento' : `${data.total} movimientos`}
            </p>
            {data.totalPages > 1 && (
              <div className="flex gap-2">
                <button
                  type="button"
                  className="btn"
                  disabled={filters.page <= 1}
                  onClick={() => list.setPage(filters.page - 1)}
                >
                  Anterior
                </button>
                <button
                  type="button"
                  className="btn"
                  disabled={filters.page >= data.totalPages}
                  onClick={() => list.setPage(filters.page + 1)}
                >
                  Siguiente
                </button>
              </div>
            )}
          </nav>
        </>
      )}
    </section>
  )
}
