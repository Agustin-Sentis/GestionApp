import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE } from '../api.ts'
import { formatDate, formatMoney } from '../format.ts'

export default function TransactionList({ year, month }: { year: number; month: number }) {
  const queryClient = useQueryClient()

  const { data, isLoading, error } = useQuery({
    queryKey: ['transactions', year, month],
    queryFn: () => api.transactions.list(year, month),
  })

  const remove = useMutation({
    mutationFn: (id: string) => api.transactions.remove(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['transactions'] })
      void queryClient.invalidateQueries({ queryKey: ['report'] })
    },
  })

  return (
    <section className="card">
      <h2 className="font-semibold">Movimientos</h2>

      {isLoading && <p className="muted">Cargando…</p>}
      {error && <p className="error-text">{error instanceof Error ? error.message : 'Error al cargar'}</p>}
      {remove.error && <p className="error-text">{remove.error.message}</p>}
      {data && data.length === 0 && <p className="muted">No hay movimientos en este mes.</p>}

      {data && data.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-sm">
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
              {data.map((t) => (
                <tr key={t.id} className="border-t border-slate-200 dark:border-slate-800">
                  <td className="p-2">{formatDate(t.date)}</td>
                  <td className="p-2">{t.categoryName}</td>
                  <td className="muted p-2">{t.note ?? '—'}</td>
                  <td
                    className={`p-2 text-right whitespace-nowrap tabular-nums ${
                      t.type === EXPENSE ? 'text-red-700 dark:text-red-400' : 'text-green-700 dark:text-green-400'
                    }`}
                  >
                    {t.type === EXPENSE ? '−' : '+'}
                    {formatMoney(t.amount, t.currency)}
                  </td>
                  <td className="p-2">
                    <button
                      type="button"
                      className="btn-icon"
                      onClick={() => remove.mutate(t.id)}
                      disabled={remove.isPending}
                      aria-label={`Eliminar movimiento de ${t.categoryName}`}
                      title="Eliminar"
                    >
                      ✕
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
