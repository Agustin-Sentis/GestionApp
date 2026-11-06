import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE, INCOME } from '../api.ts'
import type { TransactionType } from '../api.ts'
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
      <h2 className="font-semibold">Categorías</h2>

      <form className="flex items-end gap-2" onSubmit={onSubmit}>
        <label className="field flex-1">
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
        {categories.map((c) => (
          <li
            key={c.id}
            className={`inline-flex items-center gap-1 rounded-full border py-1 pr-1.5 pl-3 text-sm ${
              c.type === EXPENSE
                ? 'border-red-300 dark:border-red-900'
                : 'border-green-300 dark:border-green-900'
            }`}
          >
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
