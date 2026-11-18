import { useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, EXPENSE, INCOME } from '../../Services/api/api.ts'
import type { TransactionType } from '../../Services/api/api.ts'
import { categoryOrder, colorOf } from '../../shared/chartData.ts'
import { FORM_ERROR_MESSAGE, validateCategoryName } from '../../shared/Validations/validations.ts'
import ConfirmDelete from '../ConfirmDelete/ConfirmDelete.tsx'
import FieldError from '../FieldError/FieldError.tsx'
import { Skeleton, SkeletonGroup } from '../Skeleton/Skeleton.tsx'
import { useToast } from '../../Services/toastContext/ToastContext.ts'

const CategoryManager = () => {
  const [name, setName] = useState('')
  const [type, setType] = useState<TransactionType>(EXPENSE)
  // What is wrong with the name, found before anything is sent
  const [nameError, setNameError] = useState('')

  const queryClient = useQueryClient()
  const toast = useToast()
  const { data: categories = [], isLoading } = useQuery({
    queryKey: ['categories'],
    queryFn: api.categories.list,
  })

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

  const handleChangeName = (e: ChangeEvent<HTMLInputElement>) => {
    setName(e.target.value)
    setNameError('')
  }

  const handleChangeType = (e: ChangeEvent<HTMLSelectElement>) => setType(e.target.value as TransactionType)

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()

    const message = validateCategoryName(name)
    setNameError(message)
    if (message) {
      toast.error(FORM_ERROR_MESSAGE)
      return
    }

    create.mutate()
  }

  const handleRemoveCategory = (id: string) => () => remove.mutate(id)

  const order = categoryOrder(categories)

  return (
    <section className="card">
      <h2 className="panel-title">Categorías</h2>

      {/* The name takes a whole row; the type and the button share the one below. Three things in a row do not fit in a narrow panel. */}
      <form className="grid grid-cols-[minmax(0,1fr)_auto] items-end gap-x-3 gap-y-2" onSubmit={handleSubmit} noValidate>
        <label className="field col-span-2">
          Nombre
          <input
            className="input"
            type="text"
            value={name}
            onChange={handleChangeName}
            aria-invalid={nameError ? true : undefined}
            maxLength={60}
            required
          />
        </label>
        <FieldError className="col-span-2">{nameError}</FieldError>
        <label className="field">
          Tipo
          <select className="input" value={type} onChange={handleChangeType}>
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
              onConfirm={handleRemoveCategory(c.id)}
              pending={remove.isPending}
            />
          </li>
        ))}
      </ul>
    </section>
  )
}

export default CategoryManager
