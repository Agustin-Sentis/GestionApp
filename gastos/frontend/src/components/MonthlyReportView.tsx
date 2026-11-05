import { useQuery } from '@tanstack/react-query'
import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'
import { api, EXPENSE, INCOME } from '../api.ts'
import { formatMoney } from '../format.ts'

const COLORS = ['#0f766e', '#d97706', '#4f46e5', '#be185d', '#0284c7', '#65a30d', '#9333ea', '#dc2626']

export default function MonthlyReportView({ year, month }: { year: number; month: number }) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['report', year, month],
    queryFn: () => api.reports.monthly(year, month),
  })

  if (isLoading) return <section className="card"><p className="muted">Cargando reporte…</p></section>
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
        <div className="w-full">
          <ResponsiveContainer width="100%" height={280}>
            <PieChart>
              <Pie data={expenses} dataKey="total" nameKey="name" outerRadius={95} innerRadius={50} paddingAngle={2}>
                {expenses.map((_, i) => (
                  <Cell key={i} fill={COLORS[i % COLORS.length]} />
                ))}
              </Pie>
              <Tooltip formatter={(value) => formatMoney(Number(value))} />
              <Legend />
            </PieChart>
          </ResponsiveContainer>
        </div>
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
