import { useState } from 'react'
import type { ReactNode } from 'react'
import type { Transaction } from '../api.ts'
import { useAuth } from '../auth.tsx'
import { MAX_MONTH, MIN_MONTH, addMonths, parseMonthValue } from '../format.ts'
import { useListFilters } from '../useListFilters.ts'
import Brand, { APP_NAME, BrandMark } from '../components/Brand.tsx'
import CategoryManager from '../components/CategoryManager.tsx'
import DollarTypeToggle from '../components/DollarTypeToggle.tsx'
import MonthStats from '../components/MonthStats.tsx'
import MonthlyReportView from '../components/MonthlyReportView.tsx'
import ThemeToggle from '../components/ThemeToggle.tsx'
import TransactionForm from '../components/TransactionForm.tsx'
import TransactionList from '../components/TransactionList.tsx'

/** The three parts of the screen. On a wide screen they sit side by side; on a narrow one each is a tab. */
type View = 'resumen' | 'libro' | 'anotar'

const VIEWS: readonly { id: View; label: string }[] = [
  { id: 'resumen', label: 'Resumen' },
  { id: 'libro', label: 'Libro' },
  { id: 'anotar', label: 'Anotar' },
]

/**
 * One part of the screen. It scrolls on its own, so the page itself never does: the header and the tabs stay where they are.
 * All three are always in the page; the narrow layout only hides the ones that are not the current tab.
 */
function Panel({ id, current, children }: { id: View; current: View; children: ReactNode }) {
  return (
    <div
      role="tabpanel"
      id={`panel-${id}`}
      aria-labelledby={`tab-${id}`}
      className={`min-h-0 min-w-0 flex-col gap-3 overflow-x-hidden overflow-y-auto overscroll-contain xl:flex ${current === id ? 'flex' : 'hidden'}`}
    >
      {children}
    </div>
  )
}

export default function DashboardPage() {
  const { logout } = useAuth()
  // The transaction loaded in the side form, if any. Kept here because the list starts the edit and the form ends it.
  const [editing, setEditing] = useState<Transaction | null>(null)
  const [view, setView] = useState<View>('resumen')
  // The month (and the list's filters) live in the URL, so a reload or a shared link shows the same view
  const { filters, setMonth } = useListFilters()
  const { year, month } = parseMonthValue(filters.month)

  function startEditing(transaction: Transaction) {
    setEditing(transaction)
    // On a narrow screen the form is another tab: take the user to it
    setView('anotar')
  }

  return (
    <div className="flex h-dvh overflow-hidden">
      {/* The margin of the notebook page: a double red line with the name written along it. Only where there is room. */}
      <aside
        aria-hidden="true"
        className="hidden w-14 shrink-0 flex-col items-center gap-5 border-r-4 border-double border-margin bg-paper/80 py-4 xl:flex"
      >
        <BrandMark />
        <span className="display rotate-180 text-sm uppercase [writing-mode:vertical-rl]" style={{ letterSpacing: '0.3em' }}>
          {APP_NAME}
        </span>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex shrink-0 flex-wrap items-center gap-x-6 gap-y-3 border-b-4 border-double border-margin bg-paper/80 px-3 py-3 sm:px-5 xl:border-b-2">
          <h1 className="sr-only">{APP_NAME}: gestor de gastos</h1>
          {/*
            On a phone: the name and the controls share the first row, then the month, then the figures.
            From lg up everything is one row, in this order: name (until the side margin has it), month, figures, controls.
          */}
          <Brand className="order-1 xl:hidden" />

          {/* min-w-0 and max-w-full: on a very narrow screen the month field shrinks instead of pushing the page wider */}
          <div className="order-3 inline-flex max-w-full min-w-0 items-center gap-1 lg:order-2">
            <button
              type="button"
              className="btn shrink-0 px-2.5"
              onClick={() => setMonth(addMonths(filters.month, -1))}
              disabled={filters.month <= MIN_MONTH}
              aria-label="Mes anterior"
            >
              ‹
            </button>
            <input
              // Wide enough for the longest month ("septiembre de 2026") and the calendar icon
              className="input w-48 min-w-0 shrink"
              type="month"
              value={filters.month}
              onChange={(e) => e.target.value && setMonth(e.target.value)}
              aria-label="Mes a visualizar"
            />
            <button
              type="button"
              className="btn shrink-0 px-2.5"
              onClick={() => setMonth(addMonths(filters.month, 1))}
              disabled={filters.month >= MAX_MONTH}
              aria-label="Mes siguiente"
            >
              ›
            </button>
          </div>

          {/* Under the other controls on a phone; beside them on a wide screen */}
          <div className="order-4 basis-full lg:order-3 lg:min-w-[22rem] lg:max-w-xl lg:flex-1 lg:basis-auto">
            <MonthStats year={year} month={month} />
          </div>

          {/* Wraps: with no room for both, "Salir" goes under the theme control instead of being cut off */}
          <div className="order-2 ml-auto flex min-w-0 flex-wrap items-center justify-end gap-3 lg:order-4">
            <DollarTypeToggle />
            <ThemeToggle />
            <button type="button" className="btn" onClick={logout}>Salir</button>
          </div>
        </header>

        <main className="grid min-h-0 flex-1 gap-3 p-3 sm:p-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)_minmax(0,0.85fr)]">
          <Panel id="resumen" current={view}>
            <MonthlyReportView year={year} month={month} />
          </Panel>

          <Panel id="libro" current={view}>
            <TransactionList
              onEdit={startEditing}
              editingId={editing?.id ?? null}
              // The row being edited is gone: go back to creating
              onDeleted={(id) => setEditing((current) => (current?.id === id ? null : current))}
            />
          </Panel>

          <Panel id="anotar" current={view}>
            {/* The key makes React start a fresh form for each transaction (and for "new"), with its own values */}
            <TransactionForm key={editing?.id ?? 'new'} editing={editing} onFinishEditing={() => setEditing(null)} />
            <CategoryManager />
          </Panel>
        </main>

        {/* Where the three parts do not fit side by side, they are chosen here */}
        <nav aria-label="Secciones" className="shrink-0 border-t-4 border-double border-margin bg-sheet xl:hidden">
          <div role="tablist" className="grid grid-cols-3">
            {VIEWS.map((option) => (
              <button
                key={option.id}
                type="button"
                role="tab"
                id={`tab-${option.id}`}
                aria-selected={view === option.id}
                aria-controls={`panel-${option.id}`}
                onClick={() => setView(option.id)}
                // A third of a narrow screen is not much: normal-width letters and a smaller size there, the wide ones from sm up.
                // min-w-0 and truncate are the last resort, so the labels can never be drawn over each other.
                className={`display min-w-0 cursor-pointer truncate px-1 py-3 text-[0.7rem] tracking-[0.04em] uppercase [font-stretch:100%] sm:text-sm sm:[font-stretch:125%] ${
                  view === option.id ? 'bg-mark text-on-mark' : 'text-ink-soft hover:text-ink'
                }`}
              >
                {option.label}
              </button>
            ))}
          </div>
        </nav>
      </div>
    </div>
  )
}
