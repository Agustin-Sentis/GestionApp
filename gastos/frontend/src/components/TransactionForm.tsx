import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE, INCOME } from '../api.ts'
import type { Currency, Transaction, TransactionInput } from '../api.ts'
import { defaultDateForMonth, formatMonth } from '../format.ts'
import { useListFilters } from '../useListFilters.ts'
import { useToast } from './toastContext.ts'

interface Props {
  /** The transaction being edited. Without it the form creates a new one. */
  editing?: Transaction | null
  /** Called when editing ends, saved or cancelled. */
  onFinishEditing?: () => void
}

/**
 * Creates a transaction, or edits one. The form starts from `editing` when it mounts, so whoever uses it
 * gives it a `key` that changes with the transaction (the dashboard does) to load a different one.
 */
export default function TransactionForm({ editing = null, onFinishEditing }: Props) {
  const queryClient = useQueryClient()
  const toast = useToast()
  const { data: categories = [] } = useQuery({
    queryKey: ['categories'],
    queryFn: api.categories.list,
  })

  const [amount, setAmount] = useState(editing ? String(editing.amount) : '')
  const [currency, setCurrency] = useState<Currency>(editing?.currency ?? 'ARS')
  // The month the page is showing: a new movement starts inside it, and the notice says so if it ends up elsewhere
  const { filters, setMonth } = useListFilters()
  const [date, setDate] = useState(editing?.date ?? defaultDateForMonth(filters.month))
  // Once the user picks a date themselves, changing the month above must not overwrite it
  const dateTouched = useRef(false)
  const [categoryId, setCategoryId] = useState(editing?.categoryId ?? '')
  const [note, setNote] = useState(editing?.note ?? '')

  const formRef = useRef<HTMLFormElement>(null)
  const amountRef = useRef<HTMLInputElement>(null)

  // Starting an edit: bring the form into view (on a phone it sits below the list) and put the cursor in the amount
  useEffect(() => {
    if (!editing) return
    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
    formRef.current?.scrollIntoView?.({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'center' })
    amountRef.current?.focus()
  }, [editing])

  // Looking at another month: move the suggested date along with it, unless the user already chose one
  useEffect(() => {
    if (editing || dateTouched.current) return
    setDate(defaultDateForMonth(filters.month))
  }, [filters.month, editing])

  const save = useMutation({
    // Update answers with nothing and create with the new transaction; neither answer is used here
    mutationFn: async (input: TransactionInput) => {
      if (editing) await api.transactions.update(editing.id, input)
      else await api.transactions.create(input)
    },
    onSuccess: (_result, input) => {
      void queryClient.invalidateQueries({ queryKey: ['transactions'] })
      void queryClient.invalidateQueries({ queryKey: ['report'] })

      // A movement belongs to the month of its date. If that is not the month on screen it will not appear in the
      // list, so say where it went and offer to go there.
      const savedMonth = input.date.slice(0, 7)
      const elsewhere = savedMonth !== filters.month
      const where = elsewhere ? ` en ${formatMonth(savedMonth)}` : ''
      const options = elsewhere ? { action: { label: 'Ver', onClick: () => setMonth(savedMonth) } } : undefined

      if (editing) {
        toast.success(`Movimiento actualizado${where}`, options)
        onFinishEditing?.()
      } else {
        setAmount('')
        setNote('')
        toast.success(`Movimiento agregado${where}`, options)
      }
    },
  })

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    save.mutate({
      amount: Number(amount),
      currency,
      date,
      note: note.trim() || null,
      categoryId,
    })
  }

  const expenses = categories.filter((c) => c.type === EXPENSE)
  const incomes = categories.filter((c) => c.type === INCOME)

  return (
    <form
      ref={formRef}
      className="card"
      onSubmit={onSubmit}
      onKeyDown={(e) => {
        if (editing && e.key === 'Escape') onFinishEditing?.()
      }}
    >
      <h2 className="panel-title">{editing ? 'Editar movimiento' : 'Nuevo movimiento'}</h2>

      {categories.length === 0 && (
        <p className="muted text-sm">Primero creá al menos una categoría (más abajo).</p>
      )}

      <div className="flex items-end gap-2">
        <label className="field flex-1">
          Monto
          <input
            ref={amountRef}
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
        <label className="field">
          Moneda
          <select className="input" value={currency} onChange={(e) => setCurrency(e.target.value as Currency)}>
            <option value="ARS">ARS</option>
            <option value="USD">USD</option>
          </select>
        </label>
      </div>

      <label className="field">
        Categoría
        <select className="input" value={categoryId} onChange={(e) => setCategoryId(e.target.value)} required>
          <option value="" disabled>Elegí una categoría</option>
          {expenses.length > 0 && (
            <optgroup label="Gastos">
              {expenses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </optgroup>
          )}
          {incomes.length > 0 && (
            <optgroup label="Ingresos">
              {incomes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </optgroup>
          )}
        </select>
      </label>

      <label className="field">
        Fecha
        <input
          className="input"
          type="date"
          value={date}
          onChange={(e) => {
            dateTouched.current = true
            setDate(e.target.value)
          }}
          required
        />
      </label>

      <label className="field">
        Nota (opcional)
        <input className="input" type="text" value={note} onChange={(e) => setNote(e.target.value)} maxLength={200} />
      </label>

      {editing?.currency === 'USD' && currency === 'USD' && (
        <p className="muted text-sm">
          La cotización se guardó al cargar el movimiento. Si cambiás el monto o la fecha, se toma la de hoy.
        </p>
      )}

      {save.error && <p className="error-text" role="alert">{save.error.message}</p>}

      <div className="flex gap-2">
        <button type="submit" className="btn-primary flex-1" disabled={save.isPending || categories.length === 0}>
          {save.isPending ? 'Guardando…' : editing ? 'Guardar cambios' : 'Agregar'}
        </button>
        {editing && (
          <button type="button" className="btn" onClick={onFinishEditing} disabled={save.isPending}>
            Cancelar
          </button>
        )}
      </div>
    </form>
  )
}
