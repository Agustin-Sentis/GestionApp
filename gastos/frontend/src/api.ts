const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'

// ---------- Tipos (espejo de los DTOs del backend; los enums viajan como texto) ----------

export type TransactionType = 'Expense' | 'Income'
export const EXPENSE: TransactionType = 'Expense'
export const INCOME: TransactionType = 'Income'

export type Currency = 'ARS' | 'USD'

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

export interface MonthlyReport {
  year: number
  month: number
  currency: string
  income: number
  expense: number
  balance: number
  byCategory: CategoryTotal[]
  usdRateUsed: number | null
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

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
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

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T)
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
    list: (year: number, month: number) =>
      request<Transaction[]>(`/api/transaction?year=${year}&month=${month}`),
    create: (input: TransactionInput) => request<Transaction>('/api/transaction', post(input)),
    update: (id: string, input: TransactionInput) =>
      request<void>(`/api/transaction/${id}`, put(input)),
    remove: (id: string) => request<void>(`/api/transaction/${id}`, { method: 'DELETE' }),
  },

  reports: {
    monthly: (year: number, month: number) =>
      request<MonthlyReport>(`/api/report/monthly?year=${year}&month=${month}`),
  },
}
