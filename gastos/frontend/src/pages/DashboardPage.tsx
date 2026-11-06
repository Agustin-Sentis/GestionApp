import { useState } from 'react'
import type { Transaction } from '../api.ts'
import { useAuth } from '../auth.tsx'
import { MAX_MONTH, MIN_MONTH, addMonths, parseMonthValue } from '../format.ts'
import { useListFilters } from '../useListFilters.ts'
import CategoryManager from '../components/CategoryManager.tsx'
import MonthlyReportView from '../components/MonthlyReportView.tsx'
import ThemeToggle from '../components/ThemeToggle.tsx'
import TransactionForm from '../components/TransactionForm.tsx'
import TransactionList from '../components/TransactionList.tsx'

export default function DashboardPage() {
  const { logout } = useAuth()
  // The transaction loaded in the side form, if any. Kept here because the list starts the edit and the form ends it.
  const [editing, setEditing] = useState<Transaction | null>(null)
  // The month (and the list's filters) live in the URL, so a reload or a shared link shows the same view
  const { filters, setMonth } = useListFilters()
  const { year, month } = parseMonthValue(filters.month)

  return (
    <div className="mx-auto max-w-6xl px-4 pb-8">
      <header className="flex flex-wrap items-center justify-between gap-4 py-4">
        <h1 className="text-xl font-semibold">Gestor de gastos</h1>
        <div className="flex flex-wrap items-center gap-2">
          <div className="inline-flex items-center gap-1">
            <button
              type="button"
              className="btn"
              onClick={() => setMonth(addMonths(filters.month, -1))}
              disabled={filters.month <= MIN_MONTH}
              aria-label="Mes anterior"
            >
              ‹
            </button>
            <input
              className="input"
              type="month"
              value={filters.month}
              onChange={(e) => e.target.value && setMonth(e.target.value)}
              aria-label="Mes a visualizar"
            />
            <button
              type="button"
              className="btn"
              onClick={() => setMonth(addMonths(filters.month, 1))}
              disabled={filters.month >= MAX_MONTH}
              aria-label="Mes siguiente"
            >
              ›
            </button>
          </div>
          <ThemeToggle />
          <button type="button" className="btn" onClick={logout}>Cerrar sesión</button>
        </div>
      </header>

      <main className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <section className="flex min-w-0 flex-col gap-4">
          <MonthlyReportView year={year} month={month} />
          <TransactionList
            onEdit={setEditing}
            editingId={editing?.id ?? null}
            // The row being edited is gone: go back to creating
            onDeleted={(id) => setEditing((current) => (current?.id === id ? null : current))}
          />
        </section>

        <aside className="flex min-w-0 flex-col gap-4">
          {/* The key makes React start a fresh form for each transaction (and for "new"), with its own values */}
          <TransactionForm key={editing?.id ?? 'new'} editing={editing} onFinishEditing={() => setEditing(null)} />
          <CategoryManager />
        </aside>
      </main>
    </div>
  )
}
