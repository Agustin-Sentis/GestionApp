import type { Category, PagedResult, Transaction } from '../api.ts'

export const food: Category = { id: '3f2b1c9e-7a4d-4e8b-9c1d-2a5b6c7d8e9f', name: 'Comida', type: 'Expense' }
export const salary: Category = { id: '8a1d2c3b-4e5f-4a6b-8c7d-9e0f1a2b3c4d', name: 'Sueldo', type: 'Income' }

/** A valid ARS expense; pass only what a test cares about. */
export function transaction(overrides: Partial<Transaction> = {}): Transaction {
  return {
    id: 't1',
    amount: 1500,
    currency: 'ARS',
    exchangeRate: null,
    arsAmount: 1500,
    dollarType: 'Official',
    date: '2026-11-05',
    note: 'almuerzo',
    categoryId: food.id,
    categoryName: food.name,
    type: 'Expense',
    ...overrides,
  }
}

/** The server's page envelope around some items. Totals default to "this is the only page". */
export function pageOf(items: Transaction[], overrides: Partial<PagedResult<Transaction>> = {}): PagedResult<Transaction> {
  return { items, total: items.length, page: 1, pageSize: 20, totalPages: items.length > 0 ? 1 : 0, ...overrides }
}
