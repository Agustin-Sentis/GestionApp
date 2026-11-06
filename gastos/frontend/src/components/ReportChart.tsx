import { Bar, BarChart, Cell, LabelList, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { ChartView, Slice } from '../chartData.ts'
import { formatMoney, formatPercent } from '../format.ts'

type DrawnView = Exclude<ChartView, { shape: 'empty' }>

interface TooltipProps {
  active?: boolean
  payload?: { payload?: Slice }[]
  percentLabel: string
}

/** What the mouse is over: the name, the amount and the share. The table below says the same, so this only adds convenience. */
function SliceTooltip({ active, payload, percentLabel }: TooltipProps) {
  const slice = payload?.[0]?.payload
  if (!active || !slice) return null

  return (
    <div className="rounded-sm border-2 border-ink bg-sheet px-3 py-2 text-sm text-ink">
      <div className="font-semibold">{slice.name}</div>
      <div className="muted">
        {formatMoney(slice.value)} · {formatPercent(slice.percent)} {percentLabel.slice(2)}
      </div>
    </div>
  )
}

/**
 * Draws the chart of a view. It lives in its own file because the chart library is most of the app's weight:
 * MonthlyReportView loads this file on demand, so the login screen and the first paint do not wait for it.
 * The chart is decoration for the eyes: the table next to it carries every figure, so it is hidden from screen readers.
 */
export default function ReportChart({ view }: { view: DrawnView }) {
  return (
    <div aria-hidden="true" className="w-full">
      {view.shape === 'donut' ? <Donut view={view} /> : <RankedBars view={view} />}
    </div>
  )
}

function Donut({ view }: { view: DrawnView }) {
  // Remounting when the figures change makes the chart start over instead of morphing from the old sectors:
  // a donut that animates between two data sets can show the old shares for a moment
  const signature = view.slices.map((s) => `${s.key}:${s.value}`).join('|')

  return (
    <ResponsiveContainer width="100%" height={260}>
      <PieChart>
        <Pie
          key={signature}
          data={view.slices}
          dataKey="value"
          nameKey="name"
          innerRadius={55}
          outerRadius={95}
          // A 2px gap in the color of the card separates the sectors, instead of a border around each one
          stroke="var(--chart-surface)"
          strokeWidth={2}
          isAnimationActive={false}
        >
          {view.slices.map((slice) => (
            <Cell key={slice.key} fill={slice.color} />
          ))}
        </Pie>
        <Tooltip content={<SliceTooltip percentLabel={view.percentLabel} />} />
      </PieChart>
    </ResponsiveContainer>
  )
}

function RankedBars({ view }: { view: DrawnView }) {
  const rows = view.slices.map((slice) => ({ ...slice, label: formatMoney(slice.value) }))
  // Room for every row plus a little air, so the plot is never squeezed into a scrollbar
  const height = rows.length * 36 + 12

  return (
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={rows} layout="vertical" margin={{ top: 0, right: 96, bottom: 0, left: 0 }} barCategoryGap={8}>
        <XAxis type="number" hide domain={[0, 'dataMax']} />
        <YAxis
          type="category"
          dataKey="name"
          width={112}
          tickLine={false}
          axisLine={{ stroke: 'var(--chart-axis)' }}
          tick={{ fill: 'var(--chart-text)', fontSize: 12 }}
        />
        <Tooltip cursor={{ fill: 'var(--chart-hover)' }} content={<SliceTooltip percentLabel={view.percentLabel} />} />
        {/* Thin bars, square on the baseline and rounded at the tip, with the amount written at the tip */}
        <Bar dataKey="value" barSize={20} radius={[0, 4, 4, 0]} isAnimationActive={false}>
          {rows.map((row) => (
            <Cell key={row.key} fill={row.color} />
          ))}
          <LabelList dataKey="label" position="right" fill="var(--chart-text)" fontSize={12} />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  )
}
