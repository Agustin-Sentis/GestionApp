import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE } from '../api.ts'
import type { BudgetProgress, BudgetStatus, Category } from '../api.ts'
import { categoryOrder, colorOf } from '../chartData.ts'
import { currentMonthValue, formatMoneyCompact, formatPercent, parseMonthValue } from '../format.ts'
import type { SettingsSection } from './SettingsModal.tsx'
import { Skeleton, SkeletonGroup } from './Skeleton.tsx'
import { useToast } from './toastContext.ts'

/** The bar's fill by state. The state is also written in words next to it: the color is never the only signal. */
const FILL: Record<BudgetStatus, string> = {
  Ok: 'bg-pen',
  Warning: 'bg-mark',
  Exceeded: 'bg-expense',
}

/** What the state says, in words. */
function statusText(item: BudgetProgress): string {
  if (item.status === 'Exceeded') return `Superado por ${formatMoneyCompact(item.spent - item.monthlyBudget)}`
  if (item.status === 'Warning') return `Cerca del límite (${formatPercent(item.usedPercent)})`
  return formatPercent(item.usedPercent)
}

const STATUS_STYLE: Record<BudgetStatus, string> = {
  Ok: 'muted',
  Warning: 'marker font-semibold',
  Exceeded: 'font-bold text-expense',
}

/** True when the month on screen is before the current one. */
function isPastMonth(year: number, month: number): boolean {
  const now = parseMonthValue(currentMonthValue())
  return year * 12 + month < now.year * 12 + now.month
}

/**
 * The monthly budgets of the expense categories, measured against the month on screen. The figures come from the same
 * report as the rest of the summary (same query key), so they always agree with it; the amounts are edited here, where
 * they are seen, and saving refreshes the report and the categories.
 */
export default function BudgetPanel({
  year,
  month,
  onOpenSettings,
}: {
  year: number
  month: number
  /** Opens the settings window; the switch of the alert mails lives there (notifications). */
  onOpenSettings: (section: SettingsSection) => void
}) {
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data: report, isLoading } = useQuery({
    queryKey: ['report', year, month],
    queryFn: () => api.reports.monthly(year, month),
  })
  const { data: categories = [] } = useQuery({ queryKey: ['categories'], queryFn: api.categories.list })
  const order = useMemo(() => categoryOrder(categories), [categories])

  // Which row is being edited, and its amount as typed
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editAmount, setEditAmount] = useState('')

  const save = useMutation({
    mutationFn: ({ id, amount }: { id: string; amount: number | null }) => api.categories.setBudget(id, amount),
    onSuccess: (_saved, { amount }) => {
      void queryClient.invalidateQueries({ queryKey: ['categories'] })
      void queryClient.invalidateQueries({ queryKey: ['report'] })
      setEditingId(null)
      toast.success(amount === null ? 'Presupuesto quitado' : 'Presupuesto guardado')
    },
  })

  if (isLoading) {
    return (
      <section className="card">
        <h2 className="panel-title">Presupuestos</h2>
        <SkeletonGroup label="Cargando los presupuestos…" className="flex flex-col gap-3">
          <Skeleton className="h-12" />
          <Skeleton className="h-12" />
        </SkeletonGroup>
      </section>
    )
  }
  // If the report failed, the summary above is the one that explains it
  if (!report) return null

  const expenseCategories = categories.filter((c) => c.type === EXPENSE)
  const withoutBudget = expenseCategories.filter((c) => c.monthlyBudget === null)

  function startEditing(item: BudgetProgress) {
    save.reset()
    setEditingId(item.categoryId)
    setEditAmount(String(item.monthlyBudget))
  }

  return (
    <section className="card">
      <h2 className="panel-title">Presupuestos</h2>

      {report.hasUnconvertedUsd && (
        <p className="error-text" role="status">
          Algunos movimientos en USD no están sumados por falta de cotización: lo gastado puede ser más.
        </p>
      )}
      {report.budgets.length > 0 && isPastMonth(year, month) && (
        <p className="muted text-sm">Un mes pasado se mide con el presupuesto que tiene hoy cada categoría.</p>
      )}

      {report.budgets.length === 0 ? (
        <p className="muted">
          {expenseCategories.length === 0
            ? 'Creá una categoría de gasto para ponerle un presupuesto mensual.'
            : 'Todavía no definiste ningún presupuesto. Elegí una categoría y cuánto querés gastar por mes.'}
        </p>
      ) : (
        <ul className="flex flex-col gap-4">
          {report.budgets.map((item, index) => (
            <BudgetRow
              key={item.categoryId}
              item={item}
              color={colorOf(item.name, order, index)}
              editing={editingId === item.categoryId}
              amount={editAmount}
              onAmountChange={setEditAmount}
              pending={save.isPending}
              onEdit={() => startEditing(item)}
              onCancel={() => setEditingId(null)}
              onSave={() => save.mutate({ id: item.categoryId, amount: Number(editAmount) })}
              onRemove={() => save.mutate({ id: item.categoryId, amount: null })}
            />
          ))}
        </ul>
      )}

      {save.error && <p className="error-text" role="alert">{save.error.message}</p>}

      {withoutBudget.length > 0 && (
        <AddBudgetForm
          categories={withoutBudget}
          pending={save.isPending}
          onAdd={(id, amount) => save.mutate({ id, amount })}
        />
      )}

      <p className="muted border-t border-line pt-3 text-sm">
        Los avisos por correo se configuran en{' '}
        <button type="button" className="btn-link" onClick={() => onOpenSettings('notifications')}>
          Ajustes › Notificaciones
        </button>
        .
      </p>
    </section>
  )
}

interface RowProps {
  item: BudgetProgress
  color: string
  editing: boolean
  amount: string
  onAmountChange: (value: string) => void
  pending: boolean
  onEdit: () => void
  onCancel: () => void
  onSave: () => void
  onRemove: () => void
}

function BudgetRow({ item, color, editing, amount, onAmountChange, pending, onEdit, onCancel, onSave, onRemove }: RowProps) {
  const spent = formatMoneyCompact(item.spent)
  const limit = formatMoneyCompact(item.monthlyBudget)

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    onSave()
  }

  return (
    <li className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between gap-2">
        <span className="inline-flex min-w-0 items-center gap-2 font-semibold">
          <span aria-hidden="true" className="size-3 shrink-0 rounded-sm" style={{ backgroundColor: color }} />
          <span className="truncate">{item.name}</span>
        </span>
        {!editing && (
          <button type="button" className="btn-link shrink-0" onClick={onEdit} aria-label={`Editar el presupuesto de ${item.name}`}>
            Editar
          </button>
        )}
      </div>

      {/* The bar stops at the end of the track even when the budget is exceeded: how much over it is says the text */}
      <div
        role="progressbar"
        aria-label={`Presupuesto de ${item.name}`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.min(100, Math.round(item.usedPercent))}
        aria-valuetext={`${spent} de ${limit}`}
        className="h-3 overflow-hidden border-2 border-ink bg-paper"
      >
        <div className={`h-full ${FILL[item.status]}`} style={{ width: `${Math.min(100, item.usedPercent)}%` }} />
      </div>

      <p className="flex flex-wrap items-baseline justify-between gap-x-3 text-sm">
        <span className="figure muted">
          {spent} de {limit}
        </span>
        <span className={STATUS_STYLE[item.status]}>{statusText(item)}</span>
      </p>

      {editing && (
        <form className="flex flex-wrap items-end gap-2" onSubmit={onSubmit}>
          <label className="field min-w-32 grow">
            Presupuesto mensual
            <input
              className="input"
              type="number"
              inputMode="decimal"
              min="0.01"
              step="0.01"
              value={amount}
              onChange={(e) => onAmountChange(e.target.value)}
              required
            />
          </label>
          <button type="submit" className="btn-primary" disabled={pending}>Guardar</button>
          <button type="button" className="btn" onClick={onCancel}>Cancelar</button>
          <button type="button" className="btn-link text-expense" onClick={onRemove} disabled={pending}>
            Quitar presupuesto
          </button>
        </form>
      )}
    </li>
  )
}

function AddBudgetForm({
  categories,
  pending,
  onAdd,
}: {
  categories: Category[]
  pending: boolean
  onAdd: (categoryId: string, amount: number) => void
}) {
  const [categoryId, setCategoryId] = useState('')
  const [amount, setAmount] = useState('')

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    onAdd(categoryId, Number(amount))
    setCategoryId('')
    setAmount('')
  }

  return (
    <form className="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto] items-end gap-x-3 gap-y-2" onSubmit={onSubmit}>
      <p className="col-span-3 text-sm font-semibold">+ Agregar presupuesto</p>
      <label className="field">
        Categoría
        <select className="input" value={categoryId} onChange={(e) => setCategoryId(e.target.value)} required>
          <option value="" disabled>Elegí…</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </select>
      </label>
      <label className="field">
        Monto por mes
        <input
          className="input"
          type="number"
          inputMode="decimal"
          min="0.01"
          step="0.01"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          required
        />
      </label>
      <button type="submit" className="btn-primary" disabled={pending}>Agregar</button>
    </form>
  )
}
