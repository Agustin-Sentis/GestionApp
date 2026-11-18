import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router-dom'
import AuthContextProvider from '../Services/authContext/AuthContextProvider.tsx'
import ToastContextProvider from '../Services/toastContext/ToastContextProvider.tsx'

/**
 * Renders a component with the same providers the app has (query cache, auth, router),
 * but with a fresh cache per test and no retries, so a failing request fails the test right away.
 */
export function renderWithProviders(ui: ReactElement, { route = '/' }: { route?: string } = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  })

  const result = render(
    <QueryClientProvider client={queryClient}>
      <AuthContextProvider>
        <ToastContextProvider>
          <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
        </ToastContextProvider>
      </AuthContextProvider>
    </QueryClientProvider>,
  )

  return { ...result, queryClient }
}
