import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { api, EXPENSE, INCOME } from '../api.ts'
import type { Transaction, TransactionType } from '../api.ts'
import { downloadBlob, exportFileName } from '../download.ts'
import { formatDate, formatMoney, formatMonth } from '../format.ts'
import { SEARCH_MAX_LENGTH, useListFilters } from '../useListFilters.ts'
import ConfirmDelete from './ConfirmDelete.tsx'
import { Skeleton, SkeletonGroup } from './Skeleton.tsx'
import { useToast } from './toastContext.ts'

/** How long the user must stop typing before the list is asked for the text, so each keystroke is not a request. */
const SEARCH_DEBOUNCE_MS = 300

interface Props {
  /** Called when the user asks to edit a row. */
  onEdit?: (transaction: Transaction) => void
  /** The row being edited, to mark it. */
  editingId?: string | null
  /** Called after a transaction was deleted, so whoever was editing it can let go. */
  onDeleted?: (id: string) => void
}

export default function TransactionList({ onEdit, editingId = null, onDeleted }: Props) {
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
      <h2 className="font-semibold">Movimientos</h2>

      <div className="flex flex-wrap items-end gap-2">
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

        <label className="field">
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

        <label className="inline-flex items-center gap-2 py-2 text-sm">
          <input
            type="checkbox"
            checked={filters.rangeMode}
            onChange={(e) => list.setRangeMode(e.target.checked)}
          />
          Rango de fechas
        </label>

        {filters.rangeMode && (
          <>
            <label className="field">
              Desde
              <input className="input" type="date" value={filters.from} onChange={(e) => list.setFrom(e.target.value)} />
            </label>
            <label className="field">
              Hasta
              <input className="input" type="date" value={filters.to} onChange={(e) => list.setTo(e.target.value)} />
            </label>
          </>
        )}

        {hasExtraFilters && (
          <button type="button" className="btn-link py-2" onClick={list.clear}>
            Limpiar filtros
          </button>
        )}

        <button
          type="button"
          className="btn ml-auto"
          onClick={() => exportCsv.mutate()}
          disabled={exportCsv.isPending || rangeInverted || !data || data.total === 0}
        >
          {exportCsv.isPending ? 'Exportando…' : 'Exportar CSV'}
        </button>
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
        <div className="flex flex-col items-start gap-1">
          <p className="muted">
            {hasExtraFilters ? 'Ningún movimiento coincide con los filtros.' : 'No hay movimientos en este mes.'}
          </p>
          {hasExtraFilters && (
            <button type="button" className="btn-link" onClick={list.clear}>
              Limpiar filtros
            </button>
          )}
        </div>
      )}

      {data && !rangeInverted && data.total > 0 && (
        <>
          <div className="overflow-x-auto" aria-busy={isPlaceholderData}>
            <table className={`w-full border-collapse text-sm ${isPlaceholderData ? 'opacity-60' : ''}`}>
              <thead>
                <tr className="muted text-left text-xs">
                  <th className="p-2 font-semibold">Fecha</th>
                  <th className="p-2 font-semibold">Categoría</th>
                  <th className="p-2 font-semibold">Nota</th>
                  <th className="p-2 text-right font-semibold">Monto</th>
                  <th aria-label="Acciones" />
                </tr>
              </thead>
              <tbody>
                {data.items.map((t) => (
                  <tr
                    key={t.id}
                    aria-current={t.id === editingId ? 'true' : undefined}
                    className={`border-t border-slate-200 dark:border-slate-800 ${
                      t.id === editingId ? 'bg-teal-50 dark:bg-teal-950/40' : ''
                    }`}
                  >
                    <td className="p-2">{formatDate(t.date)}</td>
                    <td className="p-2">{t.categoryName}</td>
                    <td className="muted p-2">{t.note ?? '—'}</td>
                    <td className="p-2 text-right whitespace-nowrap tabular-nums">
                      <div className={t.type === EXPENSE ? 'text-red-700 dark:text-red-400' : 'text-green-700 dark:text-green-400'}>
                        {t.type === EXPENSE ? '−' : '+'}
                        {formatMoney(t.amount, t.currency)}
                      </div>
                      {t.currency === 'USD' && (
                        <div className="muted text-xs">
                          {t.exchangeRate !== null && t.arsAmount !== null
                            ? `≈ ${formatMoney(t.arsAmount)} · cotización ${formatMoney(t.exchangeRate)}`
                            : 'Sin cotización todavía'}
                        </div>
                      )}
                    </td>
                    <td className="p-2 text-right whitespace-nowrap">
                      <div className="inline-flex items-center gap-2">
                        {onEdit && (
                          <button
                            type="button"
                            className="btn-link"
                            onClick={() => onEdit(t)}
                            aria-label={`Editar movimiento de ${t.categoryName}`}
                          >
                            Editar
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
                ))}
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
