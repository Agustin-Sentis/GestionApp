import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE, INCOME } from '../api.ts'
import type { TransactionType } from '../api.ts'
import { categoryOrder, colorOf } from '../chartData.ts'
import ConfirmDelete from './ConfirmDelete.tsx'
import { Skeleton, SkeletonGroup } from './Skeleton.tsx'
import { useToast } from './toastContext.ts'

export default function CategoryManager() {
  const queryClient = useQueryClient()
  const toast = useToast()
  const { data: categories = [], isLoading } = useQuery({
    queryKey: ['categories'],
    queryFn: api.categories.list,
  })

  const order = categoryOrder(categories)

  const [name, setName] = useState('')
  const [type, setType] = useState<TransactionType>(EXPENSE)

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['categories'] })

  const create = useMutation({
    mutationFn: () => api.categories.create(name.trim(), type),
    onSuccess: () => {
      void refresh()
      setName('')
      toast.success('Categoría creada')
    },
  })

  const remove = useMutation({
    mutationFn: (id: string) => api.categories.remove(id),
    onSuccess: () => {
      void refresh()
      toast.success('Categoría eliminada')
    },
    onError: (error) => toast.error(error.message),
  })

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    create.mutate()
  }

  return (
    <section className="card">
      <h2 className="panel-title">Categorías</h2>

      {/* The name takes a whole row; the type and the button share the one below. Three things in a row do not fit in a narrow panel. */}
      <form className="grid grid-cols-[minmax(0,1fr)_auto] items-end gap-x-3 gap-y-2" onSubmit={onSubmit}>
        <label className="field col-span-2">
          Nombre
          <input
            className="input"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={60}
            required
          />
        </label>
        <label className="field">
          Tipo
          <select className="input" value={type} onChange={(e) => setType(e.target.value as TransactionType)}>
            <option value={EXPENSE}>Gasto</option>
            <option value={INCOME}>Ingreso</option>
          </select>
        </label>
        <button type="submit" className="btn-primary" disabled={create.isPending}>Crear</button>
      </form>

      {create.error && <p className="error-text" role="alert">{create.error.message}</p>}

      {isLoading && (
        <SkeletonGroup label="Cargando las categorías…" className="flex flex-wrap gap-2">
          <Skeleton className="h-8 w-24 rounded-full" />
          <Skeleton className="h-8 w-20 rounded-full" />
          <Skeleton className="h-8 w-28 rounded-full" />
        </SkeletonGroup>
      )}
      {!isLoading && categories.length === 0 && <p className="muted">Todavía no tenés categorías.</p>}

      <ul className="flex flex-wrap gap-2">
        {categories.map((c, index) => (
          <li
            key={c.id}
            // The border says the type (red: expense, blue: income); the square is the color of the category in the charts
            className={`inline-flex items-center gap-2 rounded-sm border-2 py-1 pr-1.5 pl-2 text-sm font-medium ${
              c.type === EXPENSE ? 'border-expense' : 'border-income'
            }`}
          >
            <span
              aria-hidden="true"
              className="size-3 shrink-0 rounded-sm"
              style={{ backgroundColor: colorOf(c.name, order, index) }}
            />
            <span>{c.name}</span>
            <ConfirmDelete
              label={`Eliminar categoría ${c.name}`}
              onConfirm={() => remove.mutate(c.id)}
              pending={remove.isPending}
            />
          </li>
        ))}
      </ul>
    </section>
  )
}
