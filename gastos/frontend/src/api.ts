const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'

/** What the server says when a mail link expired or was already used (the same text for every kind of link). */
export const LINK_EXPIRED_MESSAGE = 'El enlace venció o ya se usó.'

// ---------- Tipos (espejo de los DTOs del backend; los enums viajan como texto) ----------

export type TransactionType = 'Expense' | 'Income'
export const EXPENSE: TransactionType = 'Expense'
export const INCOME: TransactionType = 'Income'

export type Currency = 'ARS' | 'USD'

/** Which dollar USD amounts are converted with. */
export type DollarType = 'Official' | 'Blue' | 'Mep'
export const DOLLAR_TYPES: readonly DollarType[] = ['Official', 'Blue', 'Mep']

/** What the user can change. */
export interface Settings {
  /** The dollar new USD movements are saved with. The ones already saved keep the dollar they were saved with. */
  dollarType: DollarType
  /** The address of the account: where the confirmation mail and the budget alerts go. */
  email: string
  /** The user asked for a mail when a budget reaches 80% or is exceeded. */
  budgetEmailAlerts: boolean
  /** The user opened the link of the confirmation mail. Until then no alert is sent, even with the alerts on. */
  emailConfirmed: boolean
}

export interface AuthResponse {
  token: string
  userId: string
  email: string
  expiresAtUtc: string
}

export interface Category {
  id: string
  name: string
  type: TransactionType
  /** What the user plans to spend on it each month, in pesos. Null: no budget. Only expense categories can have one. */
  monthlyBudget: number | null
}

export interface Transaction {
  id: string
  amount: number
  currency: Currency
  /**
   * USD sell rate saved when the transaction was recorded, of the dollar it was saved with (see dollarType).
   * Null for ARS, and for USD ones that have no rate of that dollar saved (it was unavailable then).
   */
  exchangeRate: number | null
  /** The amount in pesos with that rate. Null only for USD transactions that have no rate of their dollar yet. */
  arsAmount: number | null
  /** The dollar exchangeRate and arsAmount are expressed with: the one chosen when it was saved. It never changes afterwards. */
  dollarType: DollarType
  date: string // yyyy-MM-dd
  note: string | null
  categoryId: string
  categoryName: string
  type: TransactionType
}

export interface TransactionInput {
  amount: number
  currency: Currency
  date: string
  note: string | null
  categoryId: string
}

export interface CategoryTotal {
  name: string
  type: TransactionType
  total: number
}

/** How much of a category's monthly budget has been spent: under 80%, 80% up to the whole budget, or over it. */
export type BudgetStatus = 'Ok' | 'Warning' | 'Exceeded'

/** The budget of an expense category measured against what a month spent on it. */
export interface BudgetProgress {
  categoryId: string
  name: string
  monthlyBudget: number
  /** Spent in the month, in pesos, with the same rules as the rest of the report. */
  spent: number
  /** Spent as a percentage of the budget, one decimal. Can be over 100. */
  usedPercent: number
  status: BudgetStatus
}

/** The totals of the month before, to compare the month on screen against. */
export interface PreviousMonth {
  year: number
  month: number
  income: number
  expense: number
  balance: number
  /** True when some USD movements of that month were left out for lack of a rate: its totals are incomplete. */
  hasUnconvertedUsd: boolean
}

/** A dollar that had USD movements saved without a rate of their own, and today's rate that stood in for it. */
export interface PendingConversion {
  dollarType: DollarType
  /** Null when that rate could not be obtained either: those movements are left out of the totals. */
  rate: number | null
}

/**
 * Every USD movement is converted with the rate saved with it, of the dollar it was saved with: a month can mix dollars,
 * because the user may have chosen another one in between.
 */
export interface MonthlyReport {
  year: number
  month: number
  currency: string
  income: number
  expense: number
  balance: number
  byCategory: CategoryTotal[]
  /**
   * One per expense category that has a budget (also the ones with nothing spent this month), the most urgent first.
   * A past month is measured against the budget the category has now.
   */
  budgets: BudgetProgress[]
  /** The dollars whose today's rate was needed to complete the totals, for movements that were saved without their rate. */
  pendingConversions: PendingConversion[]
  /** True when some USD transactions were left out of the totals because no rate was available. */
  hasUnconvertedUsd: boolean
  /** The month before. Null only for January of the first year the app accepts. */
  previous: PreviousMonth | null
}

/** One page of a list, plus how many items match in total. */
export interface PagedResult<T> {
  items: T[]
  total: number
  page: number // 1-based
  pageSize: number
  totalPages: number
}

/** Filters of the paged transaction list. All optional. Use a month (year + month) or a range (from + to), not both. */
export interface TransactionQuery {
  year?: number
  month?: number
  from?: string // yyyy-MM-dd, included
  to?: string // yyyy-MM-dd, included
  categoryId?: string
  type?: TransactionType
  /** Text to look for inside the note, ignoring case. */
  search?: string
  page?: number
  pageSize?: number
}

// ---------- Sesión ----------

const SESSION_KEY = 'gastos.session'

export interface Session {
  token: string
  expiresAtUtc: string
}

export function loadSession(): Session | null {
  try {
    const raw = localStorage.getItem(SESSION_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    if (!session.token || new Date(session.expiresAtUtc).getTime() <= Date.now()) {
      localStorage.removeItem(SESSION_KEY)
      return null
    }
    return session
  } catch {
    return null
  }
}

export function saveSession(session: Session | null): void {
  try {
    if (session) localStorage.setItem(SESSION_KEY, JSON.stringify(session))
    else localStorage.removeItem(SESSION_KEY)
  } catch {
    // Sin localStorage (modo privado, etc.): la sesión dura solo hasta recargar
  }
}

let unauthorizedHandler: (() => void) | null = null

/** Permite que el AuthProvider cierre la sesión cuando el backend responde 401. */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler
}

// ---------- Cliente HTTP ----------

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ErrorBody {
  error?: string // formato del ExceptionHandlingMiddleware
  title?: string // ProblemDetails de ASP.NET (errores de binding)
  errors?: Record<string, string[]>
}

async function toApiError(res: Response): Promise<ApiError> {
  let message = `Error ${res.status}`
  try {
    const body = (await res.json()) as ErrorBody
    const validation = body.errors ? Object.values(body.errors).flat().join(' ') : ''
    // Los mensajes de FluentValidation vienen separados por "~"
    message = (body.error ?? (validation || body.title || message)).split('~').join(' ')
  } catch {
    // La respuesta no era JSON: queda el mensaje genérico
  }
  return new ApiError(res.status, message)
}

/** Sends the request with the session's token and returns the response, or throws an ApiError if it is not a success. */
async function send(path: string, init: RequestInit = {}): Promise<Response> {
  const session = loadSession()
  const headers = new Headers(init.headers)
  if (init.body) headers.set('Content-Type', 'application/json')
  if (session) headers.set('Authorization', `Bearer ${session.token}`)

  let res: Response
  try {
    res = await fetch(`${API_URL}${path}`, { ...init, headers })
  } catch {
    throw new ApiError(0, 'No se pudo conectar con el servidor')
  }

  if (res.status === 401 && session) unauthorizedHandler?.()
  if (!res.ok) throw await toApiError(res)

  return res
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await send(path, init)
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T)
}

/** "?a=1&b=2" with only the values that are set (undefined, null and empty text are left out), or "" if none. */
function toQueryString(params: object): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

const post = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) })
const put = (body: unknown): RequestInit => ({ method: 'PUT', body: JSON.stringify(body) })

// ---------- Endpoints ----------

export const api = {
  signIn: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/signin', post({ email, password })),

  signUp: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/signup', post({ email, password })),

  /**
   * Asks for the recovery mail. The server answers the same (204) whether the email has an account or not, so a success here
   * says nothing about the account: the screen must not either. A 400 means the email has no valid format; 429, too many tries.
   */
  forgotPassword: (email: string) => request<void>('/api/auth/forgot-password', post({ email })),

  /**
   * Sets a new password with the token of the recovery mail. It does not sign anyone in. A 400 is either the link (expired or
   * used: LINK_EXPIRED_MESSAGE) or a password that breaks the rules (the message says which; the link is still good then).
   */
  resetPassword: (token: string, newPassword: string) =>
    request<void>('/api/auth/reset-password', post({ token, newPassword })),

  categories: {
    list: () => request<Category[]>('/api/category'),
    create: (name: string, type: TransactionType) =>
      request<Category>('/api/category', post({ name, type })),
    /** Sets the monthly budget of an expense category, or removes it with null. */
    setBudget: (id: string, monthlyBudget: number | null) =>
      request<Category>(`/api/category/${id}/budget`, put({ monthlyBudget })),
    remove: (id: string) => request<void>(`/api/category/${id}`, { method: 'DELETE' }),
  },

  transactions: {
    listPaged: (query: TransactionQuery = {}) =>
      request<PagedResult<Transaction>>(`/api/transaction/paged${toQueryString(query)}`),
    /** Every transaction that matches the filters, as a CSV file. The page and page size of the query do not apply. */
    exportCsv: async (query: TransactionQuery = {}): Promise<Blob> => {
      const filters = { ...query, page: undefined, pageSize: undefined }
      return (await send(`/api/transaction/export${toQueryString(filters)}`)).blob()
    },
    get: (id: string) => request<Transaction>(`/api/transaction/${id}`),
    create: (input: TransactionInput) => request<Transaction>('/api/transaction', post(input)),
    update: (id: string, input: TransactionInput) =>
      request<void>(`/api/transaction/${id}`, put(input)),
    remove: (id: string) => request<void>(`/api/transaction/${id}`, { method: 'DELETE' }),
  },

  settings: {
    get: () => request<Settings>('/api/settings'),
    update: (dollarType: DollarType) => request<Settings>('/api/settings', put({ dollarType })),
    /**
     * Turns the budget alert mails on or off. Turning them on with an unconfirmed address sends the confirmation mail
     * (at most one per minute); the server answers 503 when that mail could not be sent, and then nothing is saved.
     */
    setBudgetAlerts: (enabled: boolean) => request<Settings>('/api/settings/budget-alerts', put({ enabled })),
    /** Confirms the address with the token of the mail's link. Needs no session. A 400 means the link expired or was used. */
    confirmEmail: (token: string) => request<void>('/api/settings/confirm-email', post({ token })),
  },

  reports: {
    monthly: (year: number, month: number) =>
      request<MonthlyReport>(`/api/report/monthly?year=${year}&month=${month}`),
  },
}
