const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'

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

  categories: {
    list: () => request<Category[]>('/api/category'),
    create: (name: string, type: TransactionType) =>
      request<Category>('/api/category', post({ name, type })),
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
  },

  reports: {
    monthly: (year: number, month: number) =>
      request<MonthlyReport>(`/api/report/monthly?year=${year}&month=${month}`),
  },
}
