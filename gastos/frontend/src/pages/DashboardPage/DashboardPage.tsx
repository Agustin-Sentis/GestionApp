import { useState } from 'react'
import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { Transaction } from '../../Services/api/api.ts'
import { useAuth } from '../../Services/authContext/AuthContext.ts'
import { MAX_MONTH, MIN_MONTH, addMonths, parseMonthValue } from '../../shared/format.ts'
import { useListFilters } from '../../shared/useListFilters.ts'
import { useSessionWatch } from '../../shared/useSessionWatch.ts'
import Brand, { APP_NAME, BrandMark } from '../../components/Brand/Brand.tsx'
import BudgetPanel from '../../components/BudgetPanel/BudgetPanel.tsx'
import CategoryManager from '../../components/CategoryManager/CategoryManager.tsx'
import DollarTypeToggle from '../../components/DollarTypeToggle/DollarTypeToggle.tsx'
import EmailStatusChip from '../../components/EmailStatusChip/EmailStatusChip.tsx'
import EvolutionView from '../../components/EvolutionView/EvolutionView.tsx'
import MonthPicker from '../../components/MonthPicker/MonthPicker.tsx'
import MonthStats from '../../components/MonthStats/MonthStats.tsx'
import SettingsButton from '../../components/SettingsButton/SettingsButton.tsx'
import SettingsModal from '../../components/SettingsModal/SettingsModal.tsx'
import type { SettingsSection } from '../../components/SettingsModal/SettingsModal.tsx'
import MonthlyReportView from '../../components/MonthlyReportView/MonthlyReportView.tsx'
import SummaryModeToggle from '../../components/SummaryModeToggle/SummaryModeToggle.tsx'
import type { SummaryMode } from '../../components/SummaryModeToggle/SummaryModeToggle.tsx'
import TransactionForm, { AMOUNT_FIELD_ID } from '../../components/TransactionForm/TransactionForm.tsx'
import TransactionList from '../../components/TransactionList/TransactionList.tsx'

/** The address parameter that says which view the summary has, and its value for the evolution (the month is the default). */
const SUMMARY_MODE_PARAM = 'vista'
const EVOLUTION_MODE = 'evolucion'

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
const Panel = ({ id, current, children }: { id: View; current: View; children: ReactNode }) => {
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

const DashboardPage = () => {
  // The transaction loaded in the side form, if any. Kept here because the list starts the edit and the form ends it.
  const [editing, setEditing] = useState<Transaction | null>(null)
  const [view, setView] = useState<View>('resumen')
  // The settings window: opened by the header button (on security) or by the link of the budgets card (on notifications)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [settingsSection, setSettingsSection] = useState<SettingsSection>('security')

  const { signOut } = useAuth()
  // Leaves on its own when the server closed this session while the tab was in the background
  useSessionWatch()
  // The month (and the list's filters) live in the URL, so a reload or a shared link shows the same view
  const { filters, setMonth } = useListFilters()
  // Also in the URL: the summary shows the month on screen, or how the months have evolved (?vista=evolucion)
  const [params, setParams] = useSearchParams()

  const handleOpenSettings = (section: SettingsSection) => {
    setSettingsSection(section)
    setSettingsOpen(true)
  }

  const handleOpenSecurity = () => handleOpenSettings('security')

  const handleCloseSettings = () => setSettingsOpen(false)

  const handleChangeSettingsSection = (section: SettingsSection) => setSettingsSection(section)

  const handleSignOut = () => signOut()

  const handlePreviousMonth = () => setMonth(addMonths(filters.month, -1))

  const handleNextMonth = () => setMonth(addMonths(filters.month, 1))

  const handleChangeMonth = (next: string) => setMonth(next)

  const handleChooseSummaryMode = (mode: SummaryMode) => {
    setParams(
      (previous) => {
        const next = new URLSearchParams(previous)
        if (mode === 'evolucion') next.set(SUMMARY_MODE_PARAM, EVOLUTION_MODE)
        else next.delete(SUMMARY_MODE_PARAM)
        return next
      },
      { replace: true },
    )
  }

  // From the empty notes: a new movement, with the cursor already in the amount (on a narrow screen, in its tab)
  const handleStartAdding = () => {
    setEditing(null)
    setView('anotar')
    requestAnimationFrame(() => document.getElementById(AMOUNT_FIELD_ID)?.focus())
  }

  const handleStartEditing = (transaction: Transaction) => {
    setEditing(transaction)
    // On a narrow screen the form is another tab: take the user to it
    setView('anotar')
  }

  // The row being edited is gone: go back to creating
  const handleDeleted = (id: string) => setEditing((current) => (current?.id === id ? null : current))

  const handleFinishEditing = () => setEditing(null)

  const handleChooseView = (next: View) => () => setView(next)

  const { year, month } = parseMonthValue(filters.month)
  const summaryMode: SummaryMode = params.get(SUMMARY_MODE_PARAM) === EVOLUTION_MODE ? 'evolucion' : 'mes'

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
        <header className="flex shrink-0 flex-wrap items-center gap-x-3 gap-y-2 sm:gap-x-6 border-b-4 border-double border-margin bg-paper/80 px-3 py-2 sm:gap-y-3 sm:px-5 sm:py-3 xl:border-b-2">
          <h1 className="sr-only">{APP_NAME}: gestor de gastos</h1>
          {/*
            On a phone: the name with the chip, the settings gear and "Salir" on the first row, then the dollar, then the month, then the figures
            (the theme is in the settings window: it used to take a row of the header). Where the dollar and the month fit side
            by side (sm), they share a row. From lg up everything is one row, in this order: name (until the side margin has
            it), month, figures, dollar, chip and "Salir".
          */}
          <Brand className="order-1 xl:hidden" />

          {/* min-w-0 and max-w-full: on a very narrow screen the month field shrinks instead of pushing the page wider */}
          <div className="order-4 inline-flex max-w-full min-w-0 items-center gap-1 lg:order-2">
            <button
              type="button"
              className="btn shrink-0 px-2.5 pointer-coarse:min-h-10 pointer-coarse:min-w-10"
              onClick={handlePreviousMonth}
              disabled={filters.month <= MIN_MONTH}
              aria-label="Mes anterior"
            >
              ‹
            </button>
            <MonthPicker
              // Wide enough for the longest month ("septiembre de 2026") and the calendar icon
              className="w-48 shrink"
              label="Mes a visualizar"
              hideLabel
              value={filters.month}
              onChange={handleChangeMonth}
            />
            <button
              type="button"
              className="btn shrink-0 px-2.5 pointer-coarse:min-h-10 pointer-coarse:min-w-10"
              onClick={handleNextMonth}
              disabled={filters.month >= MAX_MONTH}
              aria-label="Mes siguiente"
            >
              ›
            </button>
          </div>

          {/* Under the other controls on a phone; beside them on a wide screen */}
          <div className="order-5 basis-full lg:order-3 lg:min-w-[22rem] lg:max-w-xl lg:flex-1 lg:basis-auto">
            <MonthStats year={year} month={month} />
          </div>

          <div className="order-3 lg:order-4 lg:ml-auto">
            <DollarTypeToggle />
          </div>

          <div className="order-2 ml-auto flex min-w-0 items-center justify-end gap-2 sm:gap-3 lg:order-5 lg:ml-0">
            {/* Only while the address is not confirmed; it does not take up room otherwise */}
            <EmailStatusChip />
            <SettingsButton onClick={handleOpenSecurity} />
            <button type="button" className="btn pointer-coarse:min-h-10" onClick={handleSignOut}>Salir</button>
          </div>
        </header>

        <main className="grid min-h-0 flex-1 gap-3 p-3 sm:p-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)_minmax(0,0.85fr)]">
          <Panel id="resumen" current={view}>
            <SummaryModeToggle mode={summaryMode} onChange={handleChooseSummaryMode} />
            {summaryMode === 'evolucion' ? (
              // The budgets are of one month: they do not belong to the evolution
              <EvolutionView selectedMonth={filters.month} />
            ) : (
              <>
                <MonthlyReportView year={year} month={month} onAddMovement={handleStartAdding} />
                <BudgetPanel year={year} month={month} onOpenSettings={handleOpenSettings} />
              </>
            )}
          </Panel>

          <Panel id="libro" current={view}>
            <TransactionList
              onEdit={handleStartEditing}
              onAddMovement={handleStartAdding}
              editingId={editing?.id ?? null}
              onDeleted={handleDeleted}
            />
          </Panel>

          <Panel id="anotar" current={view}>
            {/* The key makes React start a fresh form for each transaction (and for "new"), with its own values */}
            <TransactionForm key={editing?.id ?? 'new'} editing={editing} onFinishEditing={handleFinishEditing} />
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
                onClick={handleChooseView(option.id)}
                // A third of a narrow screen is not much: normal-width letters and a smaller size there, the wide ones from sm up.
                // min-w-0 and truncate are the last resort, so the labels can never be drawn over each other.
                className={`display min-w-0 cursor-pointer truncate px-1 py-3 pointer-coarse:min-h-11 text-[0.7rem] tracking-[0.04em] uppercase [font-stretch:100%] sm:text-sm sm:[font-stretch:125%] ${
                  view === option.id ? 'bg-mark text-on-mark' : 'text-ink-soft hover:text-ink'
                }`}
              >
                {option.label}
              </button>
            ))}
          </div>
        </nav>
      </div>

      <SettingsModal
        open={settingsOpen}
        section={settingsSection}
        onSectionChange={handleChangeSettingsSection}
        onClose={handleCloseSettings}
      />
    </div>
  )
}

export default DashboardPage
