import { useEffect, useRef, useState } from 'react'
import type { ChangeEvent, FormEvent, KeyboardEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE, INCOME } from '../../Services/api/api.ts'
import type { Currency, Transaction, TransactionInput } from '../../Services/api/api.ts'
import { DOLLAR_NAMES } from '../../shared/dollar.ts'
import { defaultDateForMonth, formatMonth } from '../../shared/format.ts'
import { JUST_ADDED_KEY } from '../../shared/justAdded.ts'
import {
  FORM_ERROR_MESSAGE,
  hasErrors,
  validateAmount,
  validateCategoryId,
  validateDate,
  validateNote,
} from '../../shared/Validations/validations.ts'
import type { FormErrors } from '../../shared/Validations/validations.ts'
import { useListFilters } from '../../shared/useListFilters.ts'
import DatePicker from '../DatePicker/DatePicker.tsx'
import FieldError from '../FieldError/FieldError.tsx'
import { useToast } from '../../Services/toastContext/ToastContext.ts'

/** The fields of this form that can have an error under them. */
type Field = 'amount' | 'categoryId' | 'date' | 'note'

/** The id of the amount field: other parts of the page (the empty notes) take the person straight to it. */
export const AMOUNT_FIELD_ID = 'movement-amount'

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
const TransactionForm = ({ editing = null, onFinishEditing }: Props) => {
  // The month the page is showing: a new movement starts inside it (so this comes before the states), and the notice says so if it ends up elsewhere
  const { filters, setMonth } = useListFilters()

  const [amount, setAmount] = useState(editing ? String(editing.amount) : '')
  const [currency, setCurrency] = useState<Currency>(editing?.currency ?? 'ARS')
  const [date, setDate] = useState(editing?.date ?? defaultDateForMonth(filters.month))
  const [categoryId, setCategoryId] = useState(editing?.categoryId ?? '')
  const [note, setNote] = useState(editing?.note ?? '')
  // What is wrong with each field, found before anything is sent
  const [errors, setErrors] = useState<FormErrors<Field>>({})

  const queryClient = useQueryClient()
  const toast = useToast()
  // Once the user picks a date themselves, changing the month above must not overwrite it
  const dateTouched = useRef(false)
  const formRef = useRef<HTMLFormElement>(null)
  const amountRef = useRef<HTMLInputElement>(null)

  const { data: categories = [] } = useQuery({
    queryKey: ['categories'],
    queryFn: api.categories.list,
  })
  // The dollar a USD movement is saved with. Until it arrives (or if it fails) the notes say "the one you have chosen"
  const { data: settings } = useQuery({ queryKey: ['settings'], queryFn: api.settings.get })
  const chosenDollar = settings?.dollarType

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

  // What the "Ver" button of the notice does: take the person to the month where the movement went
  const handleGoToMonth = (month: string) => () => setMonth(month)

  const save = useMutation({
    // Update answers with nothing; create answers with the new transaction, whose id says which row to highlight
    mutationFn: async (input: TransactionInput) => {
      if (editing) {
        await api.transactions.update(editing.id, input)
        return null
      }
      return (await api.transactions.create(input)).id
    },
    onSuccess: (createdId, input) => {
      if (createdId) queryClient.setQueryData(JUST_ADDED_KEY, createdId)
      void queryClient.invalidateQueries({ queryKey: ['transactions'] })
      void queryClient.invalidateQueries({ queryKey: ['report'] })

      // A movement belongs to the month of its date. If that is not the month on screen it will not appear in the
      // list, so say where it went and offer to go there.
      const savedMonth = input.date.slice(0, 7)
      const elsewhere = savedMonth !== filters.month
      const where = elsewhere ? ` en ${formatMonth(savedMonth)}` : ''
      const options = elsewhere ? { action: { label: 'Ver', onClick: handleGoToMonth(savedMonth) } } : undefined

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

  const clearError = (field: Field) => setErrors((previous) => ({ ...previous, [field]: '' }))

  const handleChangeAmount = (e: ChangeEvent<HTMLInputElement>) => {
    setAmount(e.target.value)
    clearError('amount')
  }

  const handleChangeCurrency = (e: ChangeEvent<HTMLSelectElement>) => setCurrency(e.target.value as Currency)

  const handleChangeCategory = (e: ChangeEvent<HTMLSelectElement>) => {
    setCategoryId(e.target.value)
    clearError('categoryId')
  }

  const handleChangeDate = (next: string) => {
    dateTouched.current = true
    setDate(next)
    clearError('date')
  }

  const handleChangeNote = (e: ChangeEvent<HTMLInputElement>) => {
    setNote(e.target.value)
    clearError('note')
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()

    const formErrors: FormErrors<Field> = {
      amount: validateAmount(amount),
      categoryId: validateCategoryId(categoryId),
      date: validateDate(date),
      note: validateNote(note),
    }
    setErrors(formErrors)
    if (hasErrors(formErrors)) {
      toast.error(FORM_ERROR_MESSAGE)
      return
    }

    save.mutate({
      amount: Number(amount),
      currency,
      date,
      note: note.trim() || null,
      categoryId,
    })
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLFormElement>) => {
    if (editing && e.key === 'Escape') onFinishEditing?.()
  }

  const expenses = categories.filter((c) => c.type === EXPENSE)
  const incomes = categories.filter((c) => c.type === INCOME)

  return (
    <form ref={formRef} className="card" onSubmit={handleSubmit} onKeyDown={handleKeyDown} noValidate>
      <h2 className="panel-title">{editing ? 'Editar movimiento' : 'Nuevo movimiento'}</h2>

      {categories.length === 0 && (
        <p className="muted text-sm">Primero creá al menos una categoría (más abajo).</p>
      )}

      <div className="flex items-end gap-2">
        <label className="field flex-1">
          Monto
          <input
            ref={amountRef}
            id={AMOUNT_FIELD_ID}
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
        <label className="field">
          Moneda
          <select className="input" value={currency} onChange={handleChangeCurrency}>
            <option value="ARS">ARS</option>
            <option value="USD">USD</option>
          </select>
        </label>
      </div>
      <FieldError>{errors.amount}</FieldError>

      <label className="field">
        Categoría
        <select className="input" value={categoryId} onChange={handleChangeCategory} aria-invalid={errors.categoryId ? true : undefined} required>
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
      <FieldError>{errors.categoryId}</FieldError>

      <DatePicker label="Fecha" value={date} onChange={handleChangeDate} error={errors.date} />

      <label className="field">
        Nota (opcional)
        <input
          className="input"
          type="text"
          value={note}
          onChange={handleChangeNote}
          aria-invalid={errors.note ? true : undefined}
          maxLength={200}
        />
      </label>
      <FieldError>{errors.note}</FieldError>

      {/* Which rate this movement ends up with: the one chosen when it is saved, and it stays that way */}
      {currency === 'USD' && editing?.currency === 'USD' && (
        <p className="muted text-sm">
          Este movimiento se guardó con la cotización {DOLLAR_NAMES[editing.dollarType]} del día en que se cargó. Si cambiás el
          monto o la fecha, se guarda de nuevo con {chosenDollar ? `la ${DOLLAR_NAMES[chosenDollar]}` : 'la que tengas elegida'},
          la de hoy.
        </p>
      )}
      {currency === 'USD' && editing?.currency !== 'USD' && (
        <p className="muted text-sm">
          Se va a guardar con la cotización {chosenDollar ? DOLLAR_NAMES[chosenDollar] : 'que tengas elegida'} de hoy, y queda
          así aunque después elijas otro dólar.
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

export default TransactionForm
