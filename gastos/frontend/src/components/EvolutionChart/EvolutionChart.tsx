import { Bar, ComposedChart, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { EvolutionPoint } from '../../shared/evolutionData.ts'
import { formatMoney, formatMoneyCompact } from '../../shared/format.ts'

const INCOME_COLOR = 'var(--series-1)'
const EXPENSE_COLOR = 'var(--series-2)'
const BALANCE_COLOR = 'var(--ink)'

interface TooltipProps {
  active?: boolean
  payload?: { payload?: EvolutionPoint }[]
}

/** What the mouse is over: the month and its three figures. The table below says the same, so this only adds convenience. */
const MonthTooltip = ({ active, payload }: TooltipProps) => {
  const point = payload?.[0]?.payload
  if (!active || !point) return null

  return (
    <div className="rounded-sm border-2 border-ink bg-sheet px-3 py-2 text-sm text-ink">
      <div className="font-semibold">{point.label}{point.incomplete && ' (en curso)'}</div>
      <div className="muted">Ingresos {formatMoney(point.income)}</div>
      <div className="muted">Gastos {formatMoney(point.expense)}</div>
      <div className="muted">Saldo {formatMoney(point.balance)}</div>
    </div>
  )
}

const KeyItem = ({ color, label, line = false }: { color: string; label: string; line?: boolean }) => {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span
        className={line ? 'h-0.5 w-4 shrink-0' : 'size-3 shrink-0 rounded-sm'}
        style={{ backgroundColor: color }}
      />
      {label}
    </span>
  )
}

/**
 * Income and expenses of each month as bars side by side, with the balance as a line over them. It lives in its own file
 * because the chart library is most of the app's weight: EvolutionView loads this file on demand.
 * The chart is decoration for the eyes: the table next to it carries every figure, so it is hidden from screen readers.
 */
const EvolutionChart = ({ points }: { points: EvolutionPoint[] }) => {
  return (
    <div aria-hidden="true" className="flex w-full flex-col gap-2">
      <div className="muted flex flex-wrap gap-x-4 gap-y-1 text-xs">
        <KeyItem color={INCOME_COLOR} label="Ingresos" />
        <KeyItem color={EXPENSE_COLOR} label="Gastos" />
        <KeyItem color={BALANCE_COLOR} label="Saldo" line />
      </div>

      <ResponsiveContainer width="100%" height={280}>
        <ComposedChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <XAxis
            dataKey="label"
            tickLine={false}
            axisLine={{ stroke: 'var(--chart-axis)' }}
            tick={{ fill: 'var(--chart-text)', fontSize: 12 }}
          />
          <YAxis
            width={64}
            tickLine={false}
            axisLine={false}
            tick={{ fill: 'var(--chart-text)', fontSize: 12 }}
            tickFormatter={(value: number) => formatMoneyCompact(value)}
          />
          <Tooltip cursor={{ fill: 'var(--chart-hover)' }} content={<MonthTooltip />} />
          <Bar dataKey="income" fill={INCOME_COLOR} radius={[4, 4, 0, 0]} isAnimationActive={false} />
          <Bar dataKey="expense" fill={EXPENSE_COLOR} radius={[4, 4, 0, 0]} isAnimationActive={false} />
          <Line
            dataKey="balance"
            stroke={BALANCE_COLOR}
            strokeWidth={2}
            dot={{ r: 3, fill: BALANCE_COLOR, stroke: 'var(--chart-surface)' }}
            isAnimationActive={false}
          />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  )
}

export default EvolutionChart
