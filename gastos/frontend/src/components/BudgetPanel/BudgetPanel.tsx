import { useMemo, useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE } from '../../Services/api/api.ts'
import type { BudgetProgress, BudgetStatus, Category } from '../../Services/api/api.ts'
import { categoryOrder, colorOf } from '../../shared/chartData.ts'
import { currentMonthValue, formatMoneyCompact, formatPercent, parseMonthValue } from '../../shared/format.ts'
import EmptyNote from '../EmptyNote/EmptyNote.tsx'
import type { SettingsSection } from '../SettingsModal/SettingsModal.tsx'
import { Skeleton, SkeletonGroup } from '../Skeleton/Skeleton.tsx'
import FieldError from '../FieldError/FieldError.tsx'
import { FORM_ERROR_MESSAGE, hasErrors, validateBudget, validateCategoryId } from '../../shared/Validations/validations.ts'
import type { FormErrors } from '../../shared/Validations/validations.ts'
import { useToast } from '../../Services/toastContext/ToastContext.ts'

/** The bar's fill by state. The state is also written in words next to it: the color is never the only signal. */
// The stroke of the highlighter: the blue a little see-through (the pen pressed lightly), the yellow and the red at full strength
const FILL: Record<BudgetStatus, string> = {
  Ok: 'bg-pen/70',
  Warning: 'bg-mark',
  Exceeded: 'bg-expense',
}

/** What the state says, in words. */
const statusText = (item: BudgetProgress): string => {
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
const isPastMonth = (year: number, month: number): boolean => {
  const now = parseMonthValue(currentMonthValue())
  return year * 12 + month < now.year * 12 + now.month
}

interface RowProps {
  item: BudgetProgress
  color: string
  editing: boolean
  amount: string
  /** What is wrong with the amount being typed, if anything. */
  error: string
  onAmountChange: (value: string) => void
  pending: boolean
  onEdit: () => void
  onCancel: () => void
  onSave: () => void
  onRemove: () => void
}

const BudgetRow = ({ item, color, editing, amount, error, onAmountChange, pending, onEdit, onCancel, onSave, onRemove }: RowProps) => {
  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    onSave()
  }

  const handleChangeAmount = (e: ChangeEvent<HTMLInputElement>) => onAmountChange(e.target.value)

  const spent = formatMoneyCompact(item.spent)
  const limit = formatMoneyCompact(item.monthlyBudget)

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
        // A ruled line with a small tick at the end of the budget; the stroke sits on it. With nothing spent only the line is left
        className="relative h-4 border-b-2 border-ink"
      >
        <div className={`absolute bottom-0 left-0 h-3 rounded-xs ${FILL[item.status]}`} style={{ width: `${Math.min(100, item.usedPercent)}%` }} />
        <span aria-hidden="true" className="absolute right-0 bottom-0 h-3 w-0.5 bg-ink" />
      </div>

      <p className="flex flex-wrap items-baseline justify-between gap-x-3 text-sm">
        <span className="figure muted">
          {spent} de {limit}
        </span>
        <span className={STATUS_STYLE[item.status]}>{statusText(item)}</span>
      </p>

      {editing && (
        <form className="flex flex-wrap items-end gap-2" onSubmit={handleSubmit} noValidate>
          <label className="field min-w-32 grow">
            Presupuesto mensual
            <input
              className="input"
              type="number"
              inputMode="decimal"
              min="0.01"
              step="0.01"
              value={amount}
              onChange={handleChangeAmount}
              aria-invalid={error ? true : undefined}
              required
            />
          </label>
          <FieldError className="basis-full">{error}</FieldError>
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

const AddBudgetForm = ({
  categories,
  pending,
  onAdd,
}: {
  categories: Category[]
  pending: boolean
  onAdd: (categoryId: string, amount: number) => void
}) => {
  const [categoryId, setCategoryId] = useState('')
  const [amount, setAmount] = useState('')
  // What is wrong with each field, found before anything is sent
  const [errors, setErrors] = useState<FormErrors<'categoryId' | 'amount'>>({})

  const toast = useToast()

  const handleChangeCategory = (e: ChangeEvent<HTMLSelectElement>) => {
    setCategoryId(e.target.value)
    setErrors((previous) => ({ ...previous, categoryId: '' }))
  }

  const handleChangeAmount = (e: ChangeEvent<HTMLInputElement>) => {
    setAmount(e.target.value)
    setErrors((previous) => ({ ...previous, amount: '' }))
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()

    const formErrors = { categoryId: validateCategoryId(categoryId), amount: validateBudget(amount) }
    setErrors(formErrors)
    if (hasErrors(formErrors)) {
      toast.error(FORM_ERROR_MESSAGE)
      return
    }

    onAdd(categoryId, Number(amount))
    setCategoryId('')
    setAmount('')
  }

  return (
    <form className="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto] items-end gap-x-3 gap-y-2" onSubmit={handleSubmit} noValidate>
      <p className="col-span-3 text-sm font-semibold">+ Agregar presupuesto</p>
      <label className="field">
        Categoría
        <select className="input" value={categoryId} onChange={handleChangeCategory} aria-invalid={errors.categoryId ? true : undefined} required>
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
          onChange={handleChangeAmount}
          aria-invalid={errors.amount ? true : undefined}
          required
        />
      </label>
      <button type="submit" className="btn-primary" disabled={pending}>Agregar</button>
      <FieldError className="col-span-3">{errors.categoryId}</FieldError>
      <FieldError className="col-span-3">{errors.amount}</FieldError>
    </form>
  )
}

/**
 * The monthly budgets of the expense categories, measured against the month on screen. The figures come from the same
 * report as the rest of the summary (same query key), so they always agree with it; the amounts are edited here, where
 * they are seen, and saving refreshes the report and the categories.
 */
const BudgetPanel = ({
  year,
  month,
  onOpenSettings,
}: {
  year: number
  month: number
  /** Opens the settings window; the switch of the alert mails lives there (notifications). */
  onOpenSettings: (section: SettingsSection) => void
}) => {
  // Which row is being edited, and its amount as typed
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editAmount, setEditAmount] = useState('')
  // What is wrong with that amount, found before anything is sent
  const [editError, setEditError] = useState('')

  const queryClient = useQueryClient()
  const toast = useToast()

  const { data: report, isLoading } = useQuery({
    queryKey: ['report', year, month],
    queryFn: () => api.reports.monthly(year, month),
  })
  const { data: categories = [] } = useQuery({ queryKey: ['categories'], queryFn: api.categories.list })
  const order = useMemo(() => categoryOrder(categories), [categories])

  const save = useMutation({
    mutationFn: ({ id, amount }: { id: string; amount: number | null }) => api.categories.setBudget(id, amount),
    onSuccess: (_saved, { amount }) => {
      void queryClient.invalidateQueries({ queryKey: ['categories'] })
      void queryClient.invalidateQueries({ queryKey: ['report'] })
      setEditingId(null)
      toast.success(amount === null ? 'Presupuesto quitado' : 'Presupuesto guardado')
    },
  })

  const handleStartEditing = (item: BudgetProgress) => () => {
    save.reset()
    setEditingId(item.categoryId)
    setEditAmount(String(item.monthlyBudget))
    setEditError('')
  }

  const handleChangeAmount = (value: string) => {
    setEditAmount(value)
    setEditError('')
  }

  const handleCancelEditing = () => setEditingId(null)

  const handleSave = (item: BudgetProgress) => () => {
    const message = validateBudget(editAmount)
    setEditError(message)
    if (message) {
      toast.error(FORM_ERROR_MESSAGE)
      return
    }
    save.mutate({ id: item.categoryId, amount: Number(editAmount) })
  }

  const handleRemove = (item: BudgetProgress) => () => save.mutate({ id: item.categoryId, amount: null })

  const handleAdd = (id: string, amount: number) => save.mutate({ id, amount })

  const handleOpenNotifications = () => onOpenSettings('notifications')

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
        <EmptyNote>
          {expenseCategories.length === 0
            ? 'Creá una categoría de gasto para ponerle un presupuesto mensual.'
            : 'Todavía no definiste ningún presupuesto. Elegí una categoría y cuánto querés gastar por mes.'}
        </EmptyNote>
      ) : (
        <ul className="flex flex-col gap-4">
          {report.budgets.map((item, index) => (
            <BudgetRow
              key={item.categoryId}
              item={item}
              color={colorOf(item.name, order, index)}
              editing={editingId === item.categoryId}
              amount={editAmount}
              error={editingId === item.categoryId ? editError : ''}
              onAmountChange={handleChangeAmount}
              pending={save.isPending}
              onEdit={handleStartEditing(item)}
              onCancel={handleCancelEditing}
              onSave={handleSave(item)}
              onRemove={handleRemove(item)}
            />
          ))}
        </ul>
      )}

      {save.error && <p className="error-text" role="alert">{save.error.message}</p>}

      {withoutBudget.length > 0 && <AddBudgetForm categories={withoutBudget} pending={save.isPending} onAdd={handleAdd} />}

      <p className="muted border-t border-line pt-3 text-sm">
        Los avisos por correo se configuran en{' '}
        <button type="button" className="btn-link" onClick={handleOpenNotifications}>
          Ajustes › Notificaciones
        </button>
        .
      </p>
    </section>
  )
}

export default BudgetPanel
