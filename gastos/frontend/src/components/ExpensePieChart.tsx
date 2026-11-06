import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'
import type { CategoryTotal } from '../api.ts'
import { formatMoney } from '../format.ts'

const COLORS = ['#0f766e', '#d97706', '#4f46e5', '#be185d', '#0284c7', '#65a30d', '#9333ea', '#dc2626']

/**
 * The donut of expenses by category. It lives in its own file because the chart library is most of the app's weight:
 * MonthlyReportView loads this file on demand, so the login screen and the first paint do not wait for it.
 */
export default function ExpensePieChart({ expenses }: { expenses: CategoryTotal[] }) {
  return (
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
  )
}
