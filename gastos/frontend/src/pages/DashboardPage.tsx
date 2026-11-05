import { useState } from 'react'
import { useAuth } from '../auth.tsx'
import { currentMonthValue, parseMonthValue } from '../format.ts'
import CategoryManager from '../components/CategoryManager.tsx'
import MonthlyReportView from '../components/MonthlyReportView.tsx'
import TransactionForm from '../components/TransactionForm.tsx'
import TransactionList from '../components/TransactionList.tsx'

export default function DashboardPage() {
  const { logout } = useAuth()
  const [monthValue, setMonthValue] = useState(currentMonthValue)
  const { year, month } = parseMonthValue(monthValue)

  return (
    <div className="mx-auto max-w-6xl px-4 pb-8">
      <header className="flex flex-wrap items-center justify-between gap-4 py-4">
        <h1 className="text-xl font-semibold">Gestor de gastos</h1>
        <div className="flex flex-wrap items-center gap-2">
          <input
            className="input"
            type="month"
            value={monthValue}
            onChange={(e) => e.target.value && setMonthValue(e.target.value)}
            aria-label="Mes a visualizar"
          />
          <button type="button" className="btn" onClick={logout}>Cerrar sesión</button>
        </div>
      </header>

      <main className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <section className="flex min-w-0 flex-col gap-4">
          <MonthlyReportView year={year} month={month} />
          <TransactionList year={year} month={month} />
        </section>

        <aside className="flex min-w-0 flex-col gap-4">
          <TransactionForm />
          <CategoryManager />
        </aside>
      </main>
    </div>
  )
}
