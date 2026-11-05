import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE, INCOME } from '../api.ts'
import type { Currency } from '../api.ts'
import { todayLocal } from '../format.ts'

export default function TransactionForm() {
  const queryClient = useQueryClient()
  const { data: categories = [] } = useQuery({
    queryKey: ['categories'],
    queryFn: api.categories.list,
  })

  const [amount, setAmount] = useState('')
  const [currency, setCurrency] = useState<Currency>('ARS')
  const [date, setDate] = useState(todayLocal)
  const [categoryId, setCategoryId] = useState('')
  const [note, setNote] = useState('')

  const create = useMutation({
    mutationFn: api.transactions.create,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['transactions'] })
      void queryClient.invalidateQueries({ queryKey: ['report'] })
      setAmount('')
      setNote('')
    },
  })

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    create.mutate({
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
    <form className="card" onSubmit={onSubmit}>
      <h2 className="font-semibold">Nuevo movimiento</h2>

      {categories.length === 0 && (
        <p className="muted text-sm">Primero creá al menos una categoría (más abajo).</p>
      )}

      <div className="flex items-end gap-2">
        <label className="field flex-1">
          Monto
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
        <input className="input" type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
      </label>

      <label className="field">
        Nota (opcional)
        <input className="input" type="text" value={note} onChange={(e) => setNote(e.target.value)} maxLength={200} />
      </label>

      {create.error && <p className="error-text" role="alert">{create.error.message}</p>}

      <button type="submit" className="btn-primary" disabled={create.isPending || categories.length === 0}>
        {create.isPending ? 'Guardando…' : 'Agregar'}
      </button>
    </form>
  )
}
